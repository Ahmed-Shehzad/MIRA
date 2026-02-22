import * as pulumi from "@pulumi/pulumi";
import * as aws from "@pulumi/aws";
import * as awsx from "@pulumi/awsx";
import { getConfig } from "./config";

const pulumiConfig = new pulumi.Config();
const stackConfig = getConfig();
const projectName = stackConfig.projectName;
const env = stackConfig.environment;
const name = (s: string) => `${projectName}-${env}-${s}`;

// VPC
const vpc = new awsx.ec2.Vpc(name("vpc"), {
  cidrBlock: "10.0.0.0/16",
  numberOfAvailabilityZones: 2,
  natGateways: { strategy: "Single" },
  tags: { Environment: env, Project: projectName },
});

// RDS PostgreSQL
const dbSubnetGroup = new aws.rds.SubnetGroup(name("db-subnet"), {
  subnetIds: vpc.privateSubnetIds,
  tags: { Environment: env },
});

const dbSecurityGroup = new aws.ec2.SecurityGroup(name("db-sg"), {
  vpcId: vpc.vpcId,
  description: "RDS PostgreSQL security group",
  ingress: [
    {
      protocol: "tcp",
      fromPort: 5432,
      toPort: 5432,
      cidrBlocks: [vpc.vpc.cidrBlock],
      description: "PostgreSQL from VPC",
    },
  ],
  egress: [{ protocol: "-1", fromPort: 0, toPort: 0, cidrBlocks: ["0.0.0.0/0"] }],
  tags: { Environment: env },
});

const dbPasswordRaw = pulumiConfig.getSecret("dbPassword");
const dbPassword = dbPasswordRaw
  ? pulumi.output(dbPasswordRaw)
  : pulumi.secret(`mira-${env}-dev`);

const dbInstance = new aws.rds.Instance(name("db"), {
  engine: "postgres",
  engineVersion: "16.1",
  instanceClass: stackConfig.dbInstanceClass,
  allocatedStorage: stackConfig.dbAllocatedStorage,
  dbName: "hive_orders",
  username: "mira",
  password: dbPassword,
  dbSubnetGroupName: dbSubnetGroup.name,
  vpcSecurityGroupIds: [dbSecurityGroup.id],
  skipFinalSnapshot: env !== "prod",
  tags: { Environment: env },
});

// S3 buckets
const uploadsBucket = new aws.s3.BucketV2(name("uploads"), {
  tags: { Environment: env },
});

const frontendBucket = new aws.s3.BucketV2(name("frontend"), {
  tags: { Environment: env },
});

new aws.s3.BucketPublicAccessBlock(name("uploads-block"), {
  bucket: uploadsBucket.id,
  blockPublicAcls: true,
  blockPublicPolicy: true,
  ignorePublicAcls: true,
  restrictPublicBuckets: true,
});

new aws.s3.BucketPublicAccessBlock(name("frontend-block"), {
  bucket: frontendBucket.id,
  blockPublicAcls: true,
  blockPublicPolicy: true,
  ignorePublicAcls: true,
  restrictPublicBuckets: true,
});

// CloudFront logging bucket (must exist before distribution)
const cfLogsBucket = new aws.s3.BucketV2(name("cf-logs"), {
  tags: { Environment: env },
});

new aws.s3.BucketPublicAccessBlock(name("cf-logs-block"), {
  bucket: cfLogsBucket.id,
  blockPublicAcls: true,
  blockPublicPolicy: true,
  ignorePublicAcls: true,
  restrictPublicBuckets: true,
});

const callerIdentity = aws.getCallerIdentityOutput({});
const cfLogsBucketPolicy = new aws.s3.BucketPolicy(name("cf-logs-policy"), {
  bucket: cfLogsBucket.id,
  policy: pulumi
    .all([cfLogsBucket.arn, callerIdentity.accountId])
    .apply(([arn, accountId]) =>
      JSON.stringify({
        Version: "2012-10-17",
        Statement: [
          {
            Sid: "CloudFrontLogs",
            Effect: "Allow",
            Principal: { Service: "cloudfront.amazonaws.com" },
            Action: "s3:PutObject",
            Resource: `${arn}/*`,
            Condition: {
              StringLike: { "AWS:SourceArn": `arn:aws:cloudfront::${accountId}:distribution/*` },
            },
          },
        ],
      })
    ),
});

// CloudFront for frontend (created early so task definition can use for CORS)
const frontendOriginAccessIdentity = new aws.cloudfront.OriginAccessIdentity(
  name("frontend-oai"),
  { comment: `OAI for ${name("frontend")}` }
);

new aws.s3.BucketPolicy(name("frontend-policy"), {
  bucket: frontendBucket.id,
  policy: pulumi
    .all([frontendBucket.arn, frontendOriginAccessIdentity.iamArn])
    .apply(
      ([bucketArn, oaiArn]) =>
        JSON.stringify({
          Version: "2012-10-17",
          Statement: [
            {
              Sid: "CloudFrontRead",
              Effect: "Allow",
              Principal: { AWS: oaiArn },
              Action: "s3:GetObject",
              Resource: `${bucketArn}/*`,
            },
          ],
        })
    ),
});

// Frontend distribution (created after cert block when using custom domain)

// SQS dead-letter queue (failed messages after maxReceiveCount)
const sqsDlq = new aws.sqs.Queue(name("jobs-dlq"), {
  messageRetentionSeconds: 1209600,
  tags: { Environment: env },
});

// SQS queue (for MassTransit / job queue) with DLQ redrive
const sqsQueue = new aws.sqs.Queue(name("jobs"), {
  visibilityTimeoutSeconds: 300,
  messageRetentionSeconds: 1209600,
  redrivePolicy: sqsDlq.arn.apply((arn) =>
    JSON.stringify({
      deadLetterTargetArn: arn,
      maxReceiveCount: 5,
    })
  ),
  tags: { Environment: env },
});

// Cognito User Pool
const userPool = new aws.cognito.UserPool(name("user-pool"), {
  name: name("users"),
  autoVerifiedAttributes: ["email"],
  usernameAttributes: ["email"],
  passwordPolicy: {
    minimumLength: 8,
    requireLowercase: true,
    requireNumbers: true,
    requireSymbols: false,
    requireUppercase: true,
  },
  schemas: [
    { name: "email", attributeDataType: "String", required: true, mutable: true },
    { name: "custom:tenant_id", attributeDataType: "String", required: false, mutable: true },
    { name: "custom:company", attributeDataType: "String", required: false, mutable: true },
  ],
  tags: { Environment: env },
});

const userPoolClient = new aws.cognito.UserPoolClient(name("app-client"), {
  userPoolId: userPool.id,
  name: name("app"),
  generateSecret: false,
  explicitAuthFlows: [
    "ALLOW_USER_PASSWORD_AUTH",
    "ALLOW_REFRESH_TOKEN_AUTH",
    "ALLOW_USER_SRP_AUTH",
  ],
  tokenValidityUnits: {
    accessToken: "hours",
    idToken: "hours",
    refreshToken: "days",
  },
  accessTokenValidity: 1,
  idTokenValidity: 1,
  refreshTokenValidity: 30,
});

const userPoolDomain = new aws.cognito.UserPoolDomain(name("auth-domain"), {
  userPoolId: userPool.id,
  domain: `${projectName}-${env}-${pulumi.getStack().replace(/-/g, "")}`.toLowerCase(),
});

// ECR repositories
const ecrRepo = new aws.ecr.Repository(name("api"), {
  name: `${projectName}-api`,
  imageTagMutability: "MUTABLE",
  tags: { Environment: env },
});

const ecrWorkerRepo = new aws.ecr.Repository(name("worker"), {
  name: `${projectName}-wsi-worker`,
  imageTagMutability: "MUTABLE",
  tags: { Environment: env },
});

// ECS cluster and service
const cluster = new aws.ecs.Cluster(name("cluster"), {
  name: name("cluster"),
  tags: { Environment: env },
});

const albSecurityGroup = new aws.ec2.SecurityGroup(name("alb-sg"), {
  vpcId: vpc.vpcId,
  description: "ALB security group",
  ingress: [
    { protocol: "tcp", fromPort: 80, toPort: 80, cidrBlocks: ["0.0.0.0/0"] },
    { protocol: "tcp", fromPort: 443, toPort: 443, cidrBlocks: ["0.0.0.0/0"] },
  ],
  egress: [{ protocol: "-1", fromPort: 0, toPort: 0, cidrBlocks: ["0.0.0.0/0"] }],
  tags: { Environment: env },
});

const ecsSecurityGroup = new aws.ec2.SecurityGroup(name("ecs-sg"), {
  vpcId: vpc.vpcId,
  description: "ECS security group",
  ingress: [
    {
      protocol: "tcp",
      fromPort: 5000,
      toPort: 5000,
      securityGroups: [albSecurityGroup.id],
      description: "API from ALB",
    },
  ],
  egress: [{ protocol: "-1", fromPort: 0, toPort: 0, cidrBlocks: ["0.0.0.0/0"] }],
  tags: { Environment: env },
});

// Allow ECS to reach RDS
new aws.ec2.SecurityGroupRule(name("ecs-to-db"), {
  type: "ingress",
  fromPort: 5432,
  toPort: 5432,
  protocol: "tcp",
  securityGroupId: dbSecurityGroup.id,
  sourceSecurityGroupId: ecsSecurityGroup.id,
  description: "ECS to RDS",
});

const alb = new aws.lb.LoadBalancer(name("alb"), {
  loadBalancerType: "application",
  subnets: vpc.publicSubnetIds,
  securityGroups: [albSecurityGroup.id],
  tags: { Environment: env },
});

// ACM certificate + Route53 DNS validation (when domainName and hostedZoneId are set)
let certArn: pulumi.Output<string> | undefined;
if (stackConfig.domainName && stackConfig.hostedZoneId && !stackConfig.acmCertificateArn) {
  const cert = new aws.acm.Certificate(name("cert"), {
    domainName: stackConfig.domainName,
    subjectAlternativeNames: [`*.${stackConfig.domainName}`],
    validationMethod: "DNS",
    tags: { Environment: env },
  });

  const certValidation = cert.domainValidationOptions.apply((opts) =>
    opts.find((o) => o.domainName === stackConfig.domainName)
  );

  const certValidationRecord = new aws.route53.Record(name("cert-validation"), {
    zoneId: stackConfig.hostedZoneId!,
    name: certValidation.apply((v) => v?.resourceRecordName?.replace(/\.$/, "") ?? ""),
    type: certValidation.apply((v) => v?.resourceRecordType ?? "CNAME"),
    records: [certValidation.apply((v) => v?.resourceRecordValue ?? "")],
    ttl: 60,
  });

  new aws.acm.CertificateValidation(name("cert-validation"), {
    certificateArn: cert.arn,
    validationRecordFqdns: [certValidationRecord.fqdn],
  });

  certArn = cert.arn;
}

// Route53 A record for API (when domain + hosted zone)
if (stackConfig.domainName && stackConfig.hostedZoneId) {
  new aws.route53.Record(name("api-dns"), {
    zoneId: stackConfig.hostedZoneId!,
    name: stackConfig.apiSubdomain ?? "api",
    type: "A",
    aliases: [
      {
        name: alb.dnsName,
        zoneId: alb.zoneId,
        evaluateTargetHealth: true,
      },
    ],
  });
}

const hasFrontendDomain =
  !!(stackConfig.domainName && stackConfig.hostedZoneId && stackConfig.frontendSubdomain);
const frontendCertArn =
  hasFrontendDomain && (certArn || stackConfig.acmCertificateArn)
    ? (certArn ?? pulumi.output(stackConfig.acmCertificateArn!))
    : undefined;

const frontendDistribution = new aws.cloudfront.Distribution(name("frontend-cdn"), {
  enabled: true,
  isIpv6Enabled: true,
  defaultRootObject: "index.html",
  loggingConfig: {
    bucket: cfLogsBucket.bucketDomainName,
    includeCookies: false,
    prefix: "frontend/",
  },
  origins: [
    {
      domainName: frontendBucket.bucketRegionalDomainName,
      originId: "S3-frontend",
      s3OriginConfig: {
        originAccessIdentity: frontendOriginAccessIdentity.cloudfrontAccessIdentityPath,
      },
    },
  ],
  defaultCacheBehavior: {
    targetOriginId: "S3-frontend",
    viewerProtocolPolicy: "redirect-to-https",
    allowedMethods: ["GET", "HEAD", "OPTIONS"],
    cachedMethods: ["GET", "HEAD"],
    compress: true,
  },
  customErrorResponses: [
    { errorCode: 404, responseCode: 200, responsePagePath: "/index.html" },
    { errorCode: 403, responseCode: 200, responsePagePath: "/index.html" },
  ],
  restrictions: { geoRestriction: { restrictionType: "none" } },
  viewerCertificate: frontendCertArn
    ? { acmCertificateArn: frontendCertArn, sslSupportMethod: "sni-only", minimumProtocolVersion: "TLSv1.2_2021" }
    : { cloudfrontDefaultCertificate: true },
  aliases: frontendCertArn && stackConfig.frontendSubdomain
    ? [`${stackConfig.frontendSubdomain}.${stackConfig.domainName}`]
    : [],
  tags: { Environment: env },
});

if (stackConfig.domainName && stackConfig.hostedZoneId && stackConfig.frontendSubdomain) {
  new aws.route53.Record(name("frontend-dns"), {
    zoneId: stackConfig.hostedZoneId!,
    name: stackConfig.frontendSubdomain,
    type: "A",
    aliases: [
      {
        name: frontendDistribution.domainName,
        zoneId: frontendDistribution.hostedZoneId,
        evaluateTargetHealth: false,
      },
    ],
  });
}

const targetGroup = new aws.lb.TargetGroup(name("api-tg"), {
  port: 5000,
  protocol: "HTTP",
  vpcId: vpc.vpcId,
  targetType: "ip",
  healthCheck: {
    path: "/health/ready",
    healthyThreshold: 2,
    unhealthyThreshold: 3,
    timeout: 5,
    interval: 30,
  },
  tags: { Environment: env },
});

const useHttps = !!(stackConfig.acmCertificateArn || (stackConfig.domainName && stackConfig.hostedZoneId));

const listener = new aws.lb.Listener(name("http"), {
  loadBalancerArn: alb.arn,
  port: 80,
  protocol: "HTTP",
  defaultActions: useHttps
    ? [
        {
          type: "redirect",
          redirect: {
            port: "443",
            protocol: "HTTPS",
            statusCode: "HTTP_301",
          },
        },
      ]
    : [
        {
          type: "forward",
          targetGroupArn: targetGroup.arn,
        },
      ],
});

if (useHttps) {
  const certForHttps = certArn ?? stackConfig.acmCertificateArn!;
  new aws.lb.Listener(name("https"), {
    loadBalancerArn: alb.arn,
    port: 443,
    protocol: "HTTPS",
    certificateArn: certForHttps,
    defaultActions: [
      {
        type: "forward",
        targetGroupArn: targetGroup.arn,
      },
    ],
  });
}

const logGroup = new aws.cloudwatch.LogGroup(name("api-logs"), {
  name: `/ecs/${projectName}-${env}`,
  retentionInDays: env === "prod" ? 30 : 7,
  tags: { Environment: env },
});

const workerLogGroup = new aws.cloudwatch.LogGroup(name("worker-logs"), {
  name: `/ecs/${projectName}-${env}-worker`,
  retentionInDays: env === "prod" ? 30 : 7,
  tags: { Environment: env },
});

const executionRole = new aws.iam.Role(name("ecs-execution"), {
  assumeRolePolicy: JSON.stringify({
    Version: "2012-10-17",
    Statement: [
      {
        Action: "sts:AssumeRole",
        Effect: "Allow",
        Principal: { Service: "ecs-tasks.amazonaws.com" },
      },
    ],
  }),
  tags: { Environment: env },
});

new aws.iam.RolePolicyAttachment(name("ecs-exec-policy"), {
  role: executionRole.name,
  policyArn: "arn:aws:iam::aws:policy/service-role/AmazonECSTaskExecutionRolePolicy",
});

const taskRole = new aws.iam.Role(name("ecs-task"), {
  assumeRolePolicy: JSON.stringify({
    Version: "2012-10-17",
    Statement: [
      {
        Action: "sts:AssumeRole",
        Effect: "Allow",
        Principal: { Service: "ecs-tasks.amazonaws.com" },
      },
    ],
  }),
  tags: { Environment: env },
});

new aws.iam.RolePolicy(name("ecs-task-policy"), {
  role: taskRole.id,
  policy: pulumi
    .all([uploadsBucket.arn, sqsQueue.arn, sqsDlq.arn, stackConfig.awsRegion, callerIdentity.accountId])
    .apply(([bucketArn, queueArn, dlqArn, region, accountId]) =>
      JSON.stringify({
        Version: "2012-10-17",
        Statement: [
          {
            Effect: "Allow",
            Action: ["s3:PutObject", "s3:GetObject", "s3:DeleteObject"],
            Resource: `${bucketArn}/*`,
          },
          {
            Effect: "Allow",
            Action: [
              "sqs:SendMessage",
              "sqs:ReceiveMessage",
              "sqs:DeleteMessage",
              "sqs:GetQueueAttributes",
              "sqs:CreateQueue",
              "sqs:SetQueueAttributes",
            ],
            Resource: [queueArn, dlqArn],
          },
          {
            Effect: "Allow",
            Action: ["sqs:SendMessage", "sqs:ReceiveMessage", "sqs:DeleteMessage", "sqs:GetQueueAttributes"],
            Resource: `arn:aws:sqs:${region}:${accountId}:${projectName}-*`,
          },
          {
            Effect: "Allow",
            Action: [
              "logs:CreateLogGroup",
              "logs:CreateLogStream",
              "logs:PutLogEvents",
              "logs:DescribeLogStreams",
            ],
            Resource: `arn:aws:logs:${region}:${accountId}:log-group:/ecs/${projectName}-*:*`,
          },
          ...(stackConfig.enableXRayTracing
            ? [
                {
                  Effect: "Allow",
                  Action: [
                    "xray:PutTraceSegments",
                    "xray:PutTelemetryRecords",
                  ],
                  Resource: "*",
                },
              ]
            : []),
        ],
      })
    ),
});

// Store DB connection string in Secrets Manager for ECS
const dbConnectionSecret = new aws.secretsmanager.Secret(name("db-connection"), {
  name: name("db-connection"),
  tags: { Environment: env },
});

new aws.secretsmanager.SecretVersion(name("db-connection-version"), {
  secretId: dbConnectionSecret.id,
  secretString: pulumi
    .all([dbInstance.address, dbPassword])
    .apply(([host, password]) =>
      JSON.stringify({
        ConnectionStrings__DefaultConnection: `Host=${host};Port=5432;Database=hive_orders;Username=mira;Password=${password};SSL Mode=Require`,
      })
    ),
});

new aws.iam.RolePolicy(name("ecs-exec-secrets"), {
  role: executionRole.id,
  policy: dbConnectionSecret.arn.apply((arn) =>
    JSON.stringify({
      Version: "2012-10-17",
      Statement: [
        {
          Effect: "Allow",
          Action: ["secretsmanager:GetSecretValue"],
          Resource: arn,
        },
      ],
    })
  ),
});

const cognitoDomainName = userPoolDomain.domain;
const cognitoDomainUrl = pulumi.interpolate`https://${cognitoDomainName}.auth.${stackConfig.awsRegion}.amazoncognito.com`;

const taskDefinition = new aws.ecs.TaskDefinition(name("api-task"), {
  family: name("api"),
  cpu: stackConfig.ecsCpu,
  memory: stackConfig.ecsMemory,
  networkMode: "awsvpc",
  requiresCompatibilities: ["FARGATE"],
  executionRoleArn: executionRole.arn,
  taskRoleArn: taskRole.arn,
  containerDefinitions: pulumi
    .all([
      uploadsBucket.bucket,
      sqsQueue.url,
      userPool.id,
      userPoolClient.id,
      cognitoDomainUrl,
      alb.dnsName,
      frontendDistribution.domainName,
      dbConnectionSecret.arn,
    ])
    .apply(
      ([
        bucketName,
        queueUrl,
        userPoolId,
        clientId,
        cognitoUrl,
        albDns,
        cfDomain,
        secretArn,
      ]) =>
        JSON.stringify([
          {
            name: "api",
            image: `${ecrRepo.repositoryUrl}:latest`,
            essential: true,
            portMappings: [{ containerPort: 5000, protocol: "tcp" }],
            environment: [
              { name: "ASPNETCORE_ENVIRONMENT", value: env === "prod" ? "Production" : env },
              { name: "ASPNETCORE_URLS", value: "http://+:5000" },
              { name: "AWS__Region", value: stackConfig.awsRegion },
              { name: "AWS__S3__BucketName", value: bucketName },
              { name: "AWS__S3__Region", value: stackConfig.awsRegion },
              { name: "AWS__Cognito__UserPoolId", value: userPoolId },
              { name: "AWS__Cognito__ClientId", value: clientId },
              { name: "AWS__Cognito__Region", value: stackConfig.awsRegion },
              { name: "Cors__AllowedOrigins__0", value: `https://${cfDomain}` },
              { name: "Cors__AllowedOrigins__1", value: `http://${albDns}` },
              { name: "Serilog__LogGroup", value: `/ecs/${projectName}-${env}` },
              { name: "Serilog__Region", value: stackConfig.awsRegion },
              ...(stackConfig.enableXRayTracing
                ? [
                    { name: "Tracing__XRay__Enabled", value: "true" },
                    { name: "Tracing__XRay__ServiceName", value: `${projectName}-api` },
                  ]
                : []),
            ],
            secrets: [
              {
                name: "ConnectionStrings__DefaultConnection",
                valueFrom: `${secretArn}:ConnectionStrings__DefaultConnection::`,
              },
            ],
            logConfiguration: {
              logDriver: "awslogs",
              options: {
                "awslogs-group": logGroup.name,
                "awslogs-region": stackConfig.awsRegion,
              },
            },
          },
        ])
    ),
});

// CloudWatch alarm for DLQ depth (alert when messages accumulate)
const dlqAlarm = new aws.cloudwatch.MetricAlarm(name("dlq-depth-alarm"), {
  comparisonOperator: "GreaterThanThreshold",
  evaluationPeriods: 1,
  metricName: "ApproximateNumberOfMessagesVisible",
  namespace: "AWS/SQS",
  period: 300,
  statistic: "Sum",
  threshold: 0,
  dimensions: { QueueName: sqsDlq.name },
  alarmDescription: "Messages in DLQ - investigate failed job processing",
  tags: { Environment: env },
});

const service = new aws.ecs.Service(name("api"), {
  cluster: cluster.arn,
  taskDefinition: taskDefinition.arn,
  desiredCount: stackConfig.desiredCount,
  launchType: "FARGATE",
  networkConfiguration: {
    subnets: vpc.privateSubnetIds,
    securityGroups: [ecsSecurityGroup.id],
    assignPublicIp: false,
  },
  loadBalancers: [
    {
      targetGroupArn: targetGroup.arn,
      containerName: "api",
      containerPort: 5000,
    },
  ],
  tags: { Environment: env },
});

if (stackConfig.apiScalingMin != null && stackConfig.apiScalingMax != null) {
  const apiScalableTarget = new aws.appautoscaling.Target(name("api-scaling-target"), {
    maxCapacity: stackConfig.apiScalingMax,
    minCapacity: stackConfig.apiScalingMin,
    resourceId: pulumi.interpolate`service/${cluster.name}/${service.name}`,
    scalableDimension: "ecs:service:DesiredCount",
    serviceNamespace: "ecs",
  });
  new aws.appautoscaling.Policy(name("api-cpu-scaling"), {
    policyType: "TargetTrackingScaling",
    resourceId: apiScalableTarget.resourceId,
    scalableDimension: apiScalableTarget.scalableDimension,
    serviceNamespace: apiScalableTarget.serviceNamespace,
    targetTrackingScalingPolicyConfiguration: {
      predefinedMetricSpecification: {
        predefinedMetricType: "ECSServiceAverageCPUUtilization",
      },
      targetValue: 70,
      scaleInCooldown: 300,
      scaleOutCooldown: 60,
    },
  });
}

// WSI Worker (optional, ECS Fargate background task)
let workerService: aws.ecs.Service | undefined;
if (stackConfig.enableWsiWorker) {
  const workerTaskDef = new aws.ecs.TaskDefinition(name("worker-task"), {
    family: name("worker"),
    cpu: "256",
    memory: "512",
    networkMode: "awsvpc",
    requiresCompatibilities: ["FARGATE"],
    executionRoleArn: executionRole.arn,
    taskRoleArn: taskRole.arn,
    containerDefinitions: pulumi
      .all([uploadsBucket.bucket, stackConfig.awsRegion])
      .apply(([bucketName, region]) =>
        JSON.stringify([
          {
            name: "worker",
            image: `${ecrWorkerRepo.repositoryUrl}:latest`,
            essential: true,
            environment: [
              { name: "DOTNET_ENVIRONMENT", value: env === "prod" ? "Production" : env },
              { name: "AWS__Region", value: region },
              { name: "AWS__Scope", value: "hive-orders" },
              { name: "AWS__S3__BucketName", value: bucketName },
              { name: "AWS__S3__Region", value: region },
            ],
            logConfiguration: {
              logDriver: "awslogs",
              options: {
                "awslogs-group": workerLogGroup.name,
                "awslogs-region": stackConfig.awsRegion,
              },
            },
          },
        ])
      ),
  });

  workerService = new aws.ecs.Service(name("worker"), {
    cluster: cluster.arn,
    taskDefinition: workerTaskDef.arn,
    desiredCount:
      stackConfig.workerScalingMin != null && stackConfig.workerScalingMax != null
        ? stackConfig.workerScalingMin
        : 1,
    launchType: "FARGATE",
    networkConfiguration: {
      subnets: vpc.privateSubnetIds,
      securityGroups: [ecsSecurityGroup.id],
      assignPublicIp: false,
    },
    tags: { Environment: env },
  });

  if (stackConfig.workerScalingMin != null && stackConfig.workerScalingMax != null) {
    const workerScalableTarget = new aws.appautoscaling.Target(name("worker-scaling-target"), {
      maxCapacity: stackConfig.workerScalingMax,
      minCapacity: stackConfig.workerScalingMin,
      resourceId: pulumi.interpolate`service/${cluster.name}/${workerService.name}`,
      scalableDimension: "ecs:service:DesiredCount",
      serviceNamespace: "ecs",
    });
    new aws.appautoscaling.Policy(name("worker-cpu-scaling"), {
      policyType: "TargetTrackingScaling",
      resourceId: workerScalableTarget.resourceId,
      scalableDimension: workerScalableTarget.scalableDimension,
      serviceNamespace: workerScalableTarget.serviceNamespace,
      targetTrackingScalingPolicyConfiguration: {
        predefinedMetricSpecification: {
          predefinedMetricType: "ECSServiceAverageCPUUtilization",
        },
        targetValue: 70,
        scaleInCooldown: 300,
        scaleOutCooldown: 60,
      },
    });
  }
}

// CloudWatch dashboard
const dashboard = new aws.cloudwatch.Dashboard(name("dashboard"), {
  dashboardName: name("main"),
  dashboardBody: pulumi
    .all([
      alb.arn,
      sqsQueue.name,
      sqsDlq.name,
      cluster.name,
      service.name,
    ])
    .apply(([albArn, queueName, dlqName, clusterName, serviceName]) => {
      const albId = albArn.split("/").pop() ?? "";
      return JSON.stringify({
        widgets: [
          {
            type: "metric",
            x: 0,
            y: 0,
            width: 12,
            height: 6,
            properties: {
              title: "DLQ Depth",
              metrics: [
                ["AWS/SQS", "ApproximateNumberOfMessagesVisible", "QueueName", dlqName],
              ],
              view: "timeSeries",
              region: stackConfig.awsRegion,
            },
          },
          {
            type: "metric",
            x: 12,
            y: 0,
            width: 12,
            height: 6,
            properties: {
              title: "SQS Main Queue Depth",
              metrics: [
                ["AWS/SQS", "ApproximateNumberOfMessagesVisible", "QueueName", queueName],
              ],
              view: "timeSeries",
              region: stackConfig.awsRegion,
            },
          },
          {
            type: "metric",
            x: 0,
            y: 6,
            width: 12,
            height: 6,
            properties: {
              title: "ALB Request Count",
              metrics: [
                ["AWS/ApplicationELB", "RequestCount", "LoadBalancer", albId],
              ],
              view: "timeSeries",
              region: stackConfig.awsRegion,
            },
          },
          {
            type: "metric",
            x: 12,
            y: 6,
            width: 12,
            height: 6,
            properties: {
              title: "ALB Target Response Time",
              metrics: [
                ["AWS/ApplicationELB", "TargetResponseTime", "LoadBalancer", albId],
              ],
              view: "timeSeries",
              region: stackConfig.awsRegion,
            },
          },
          {
            type: "metric",
            x: 0,
            y: 12,
            width: 12,
            height: 6,
            properties: {
              title: "ECS CPU Utilization",
              metrics: [
                ["AWS/ECS", "CPUUtilization", "ClusterName", clusterName, "ServiceName", serviceName],
              ],
              view: "timeSeries",
              region: stackConfig.awsRegion,
            },
          },
          {
            type: "metric",
            x: 12,
            y: 12,
            width: 12,
            height: 6,
            properties: {
              title: "ECS Memory Utilization",
              metrics: [
                ["AWS/ECS", "MemoryUtilization", "ClusterName", clusterName, "ServiceName", serviceName],
              ],
              view: "timeSeries",
              region: stackConfig.awsRegion,
            },
          },
        ],
      });
    }),
});

// API Gateway WebSocket API (optional)
let webSocketApiId: pulumi.Output<string> | undefined;
let webSocketUrl: pulumi.Output<string> | undefined;
let webSocketConnectionsTableName: pulumi.Output<string> | undefined;
let webSocketEndpoint: pulumi.Output<string> | undefined;

if (stackConfig.enableWebSocketApi) {
  const wsConnectionsTable = new aws.dynamodb.Table(name("ws-connections"), {
    name: name("ws-connections"),
    hashKey: "connectionId",
    attributes: [
      { name: "connectionId", type: "S" },
      { name: "userKey", type: "S" },
    ],
    globalSecondaryIndexes: [
      {
        name: "gsi-user",
        hashKey: "userKey",
        rangeKey: "connectionId",
        projectionType: "ALL",
      },
    ],
    billingMode: "PAY_PER_REQUEST",
    tags: { Environment: env },
  });

  const wsLambdaRole = new aws.iam.Role(name("ws-lambda-role"), {
    assumeRolePolicy: JSON.stringify({
      Version: "2012-10-17",
      Statement: [
        {
          Action: "sts:AssumeRole",
          Effect: "Allow",
          Principal: { Service: "lambda.amazonaws.com" },
        },
      ],
    }),
    tags: { Environment: env },
  });

  new aws.iam.RolePolicyAttachment(name("ws-lambda-basic"), {
    role: wsLambdaRole.name,
    policyArn: "arn:aws:iam::aws:policy/service-role/AWSLambdaBasicExecutionRole",
  });

  new aws.iam.RolePolicy(name("ws-lambda-dynamo"), {
    role: wsLambdaRole.id,
    policy: wsConnectionsTable.arn.apply((arn) =>
      JSON.stringify({
        Version: "2012-10-17",
        Statement: [
          {
            Effect: "Allow",
            Action: ["dynamodb:PutItem", "dynamodb:DeleteItem", "dynamodb:GetItem"],
            Resource: [arn, `${arn}/index/*`],
          },
        ],
      })
    ),
  });

  const wsLambda = new aws.lambda.Function(name("ws-connect"), {
    name: name("ws-connect"),
    runtime: "nodejs20.x",
    handler: "index.handler",
    role: wsLambdaRole.arn,
    timeout: 10,
    environment: {
      variables: {
        TABLE_NAME: wsConnectionsTable.name,
        COGNITO_USER_POOL_ID: userPool.id,
      },
    },
    code: new pulumi.asset.AssetArchive({
      ".": new pulumi.asset.FileArchive("./lambda/ws-connect"),
    }),
    tags: { Environment: env },
  });

  const wsApi = new aws.apigatewayv2.Api(name("ws-api"), {
    protocolType: "WEBSOCKET",
    name: name("ws"),
    routeSelectionExpression: "$request.body.action",
    tags: { Environment: env },
  });

  const wsConnectIntegration = new aws.apigatewayv2.Integration(name("ws-connect-integration"), {
    apiId: wsApi.id,
    integrationType: "AWS_PROXY",
    integrationUri: wsLambda.invokeArn,
  });

  new aws.apigatewayv2.Route(name("ws-connect"), {
    apiId: wsApi.id,
    routeKey: "$connect",
    target: pulumi.interpolate`integrations/${wsConnectIntegration.id}`,
  });

  const wsDisconnectIntegration = new aws.apigatewayv2.Integration(name("ws-disconnect-integration"), {
    apiId: wsApi.id,
    integrationType: "AWS_PROXY",
    integrationUri: wsLambda.invokeArn,
  });

  new aws.apigatewayv2.Route(name("ws-disconnect"), {
    apiId: wsApi.id,
    routeKey: "$disconnect",
    target: pulumi.interpolate`integrations/${wsDisconnectIntegration.id}`,
  });

  const wsDefaultIntegration = new aws.apigatewayv2.Integration(name("ws-default-integration"), {
    apiId: wsApi.id,
    integrationType: "MOCK",
    requestTemplates: {
      "application/json": JSON.stringify({ statusCode: 200 }),
    },
  });

  new aws.apigatewayv2.Route(name("ws-default"), {
    apiId: wsApi.id,
    routeKey: "$default",
    target: pulumi.interpolate`integrations/${wsDefaultIntegration.id}`,
  });

  new aws.lambda.Permission(name("ws-apigw-invoke"), {
    action: "lambda:InvokeFunction",
    function: wsLambda.name,
    principal: "apigateway.amazonaws.com",
    sourceArn: pulumi.interpolate`arn:aws:execute-api:${stackConfig.awsRegion}:${callerIdentity.accountId}:${wsApi.id}/*`,
  });

  const wsStage = new aws.apigatewayv2.Stage(name("ws-stage"), {
    apiId: wsApi.id,
    name: env,
    autoDeploy: true,
    tags: { Environment: env },
  });

  webSocketApiId = wsApi.id;
  webSocketUrl = pulumi.interpolate`wss://${wsApi.id}.execute-api.${stackConfig.awsRegion}.amazonaws.com/${wsStage.name}`;
  webSocketConnectionsTableName = wsConnectionsTable.name;
  webSocketEndpoint = pulumi.interpolate`https://${wsApi.id}.execute-api.${stackConfig.awsRegion}.amazonaws.com/${wsStage.name}`;
}

// EKS GPU cluster (optional)
let eksClusterName: pulumi.Output<string> | undefined;
let eksKubeconfig: pulumi.Output<string> | undefined;
let eksWsiWorkerRoleArn: pulumi.Output<string> | undefined;

if (stackConfig.enableEksGpu) {
  const eksRole = new aws.iam.Role(name("eks-role"), {
    assumeRolePolicy: JSON.stringify({
      Version: "2012-10-17",
      Statement: [
        {
          Action: "sts:AssumeRole",
          Effect: "Allow",
          Principal: { Service: "eks.amazonaws.com" },
        },
      ],
    }),
    tags: { Environment: env },
  });
  new aws.iam.RolePolicyAttachment(name("eks-cluster-policy"), {
    role: eksRole.name,
    policyArn: "arn:aws:iam::aws:policy/AmazonEKSClusterPolicy",
  });
  new aws.iam.RolePolicyAttachment(name("eks-vpc-resource-controller"), {
    role: eksRole.name,
    policyArn: "arn:aws:iam::aws:policy/AmazonEKSVPCResourceController",
  });

  const eksCluster = new aws.eks.Cluster(name("eks-gpu"), {
    name: name("eks"),
    version: "1.31",
    roleArn: eksRole.arn,
    vpcConfig: {
      subnetIds: vpc.privateSubnetIds,
      endpointPrivateAccess: true,
      endpointPublicAccess: true,
    },
    tags: { Environment: env },
  });

  const nodeRole = new aws.iam.Role(name("eks-node-role"), {
    assumeRolePolicy: JSON.stringify({
      Version: "2012-10-17",
      Statement: [
        {
          Action: "sts:AssumeRole",
          Effect: "Allow",
          Principal: { Service: "ec2.amazonaws.com" },
        },
      ],
    }),
    tags: { Environment: env },
  });
  new aws.iam.RolePolicyAttachment(name("eks-node-worker"), {
    role: nodeRole.name,
    policyArn: "arn:aws:iam::aws:policy/AmazonEKSWorkerNodePolicy",
  });
  new aws.iam.RolePolicyAttachment(name("eks-node-cni"), {
    role: nodeRole.name,
    policyArn: "arn:aws:iam::aws:policy/AmazonEKS_CNI_Policy",
  });
  new aws.iam.RolePolicyAttachment(name("eks-node-ecr"), {
    role: nodeRole.name,
    policyArn: "arn:aws:iam::aws:policy/AmazonEC2ContainerRegistryReadOnly",
  });

  new aws.iam.RolePolicy(name("eks-node-s3-sqs"), {
    role: nodeRole.id,
    policy: pulumi
      .all([uploadsBucket.arn, sqsQueue.arn, sqsDlq.arn, stackConfig.awsRegion])
      .apply(([bucketArn, queueArn, dlqArn, region]) =>
        JSON.stringify({
          Version: "2012-10-17",
          Statement: [
            {
              Effect: "Allow",
              Action: ["s3:GetObject", "s3:PutObject", "s3:DeleteObject"],
              Resource: `${bucketArn}/*`,
            },
            {
              Effect: "Allow",
              Action: ["sqs:SendMessage", "sqs:ReceiveMessage", "sqs:DeleteMessage", "sqs:GetQueueAttributes"],
              Resource: [queueArn, dlqArn],
            },
            {
              Effect: "Allow",
              Action: ["sqs:SendMessage", "sqs:ReceiveMessage", "sqs:DeleteMessage", "sqs:GetQueueAttributes"],
              Resource: `arn:aws:sqs:${region}:${callerIdentity.accountId}:${projectName}-*`,
            },
          ],
        })
      ),
  });

  // OIDC provider and IRSA for pod-level IAM (wsi-worker service account)
  const oidcIssuer = eksCluster.identities.apply(
    (ids) => ids?.[0]?.oidcs?.[0]?.issuer ?? ""
  );
  const oidcProvider = new aws.iam.OpenIdConnectProvider(name("eks-oidc"), {
    url: oidcIssuer,
    clientIdLists: ["sts.amazonaws.com"],
    thumbprintLists: ["9e99a48a9960b14926bb7f3b02e22da2b0ab7280"],
  });
  const wsiWorkerRole = new aws.iam.Role(name("eks-wsi-worker-role"), {
    assumeRolePolicy: pulumi
      .all([oidcProvider.arn, oidcIssuer])
      .apply(([providerArn, issuer]) => {
        const issuerHost = issuer?.replace("https://", "") ?? "";
        return JSON.stringify({
          Version: "2012-10-17",
          Statement: [
            {
              Effect: "Allow",
              Principal: { Federated: providerArn },
              Action: "sts:AssumeRoleWithWebIdentity",
              Condition: {
                StringEquals: {
                  [`${issuerHost}:sub`]: "system:serviceaccount:mira:wsi-worker",
                },
              },
            },
          ],
        });
      }),
    tags: { Environment: env },
  });
  new aws.iam.RolePolicy(name("eks-wsi-worker-policy"), {
    role: wsiWorkerRole.id,
    policy: pulumi
      .all([uploadsBucket.arn, sqsQueue.arn, sqsDlq.arn, stackConfig.awsRegion, callerIdentity.accountId])
      .apply(([bucketArn, queueArn, dlqArn, region, accountId]) =>
        JSON.stringify({
          Version: "2012-10-17",
          Statement: [
            {
              Effect: "Allow",
              Action: ["s3:GetObject", "s3:PutObject", "s3:DeleteObject"],
              Resource: `${bucketArn}/*`,
            },
            {
              Effect: "Allow",
              Action: ["sqs:SendMessage", "sqs:ReceiveMessage", "sqs:DeleteMessage", "sqs:GetQueueAttributes"],
              Resource: [queueArn, dlqArn],
            },
            {
              Effect: "Allow",
              Action: ["sqs:SendMessage", "sqs:ReceiveMessage", "sqs:DeleteMessage", "sqs:GetQueueAttributes"],
              Resource: `arn:aws:sqs:${region}:${accountId}:${projectName}-*`,
            },
          ],
        })
      ),
  });

  new aws.eks.NodeGroup(name("eks-gpu-nodes"), {
    clusterName: eksCluster.name,
    nodeGroupName: name("gpu"),
    nodeRoleArn: nodeRole.arn,
    subnetIds: vpc.privateSubnetIds,
    instanceTypes: ["g4dn.xlarge"],
    scalingConfig: {
      desiredSize: 0,
      minSize: 0,
      maxSize: 4,
    },
    tags: { Environment: env },
  });

  eksClusterName = eksCluster.name;
  eksWsiWorkerRoleArn = wsiWorkerRole.arn;
  eksKubeconfig = pulumi
    .all([eksCluster.name, eksCluster.endpoint, eksCluster.certificateAuthority])
    .apply(([clusterName, endpoint, ca]) => {
      const caData = ca?.data ?? "";
      return JSON.stringify({
        apiVersion: "v1",
        kind: "Config",
        clusters: [
          {
            name: clusterName,
            cluster: { server: endpoint, "certificate-authority-data": caData },
          },
        ],
        contexts: [{ name: clusterName, context: { cluster: clusterName, user: "aws" } }],
        "current-context": clusterName,
      });
    });
}

// Outputs
export const vpcId = vpc.vpcId;
export const dbEndpoint = dbInstance.endpoint;
export const dbHost = dbInstance.address;
export const uploadsBucketName = uploadsBucket.bucket;
export const frontendBucketName = frontendBucket.bucket;
export const sqsQueueUrl = sqsQueue.url;
export const sqsDlqUrl = sqsDlq.url;
export const cognitoUserPoolId = userPool.id;
export const cognitoClientId = userPoolClient.id;
export const cognitoDomain = cognitoDomainUrl;
export const ecrRepositoryUrl = ecrRepo.repositoryUrl;
export const ecrWorkerRepositoryUrl = ecrWorkerRepo.repositoryUrl;
export const ecsClusterName = cluster.name;
export const ecsServiceName = service.name;
export const ecsWorkerServiceName = workerService?.name;
export const albDnsName = alb.dnsName;
export const albUrl = alb.dnsName.apply((d) => `http://${d}`);
export const cloudFrontDomain = frontendDistribution.domainName;
export const cloudFrontUrl = frontendDistribution.domainName.apply(
  (d) => `https://${d}`
);
export const cloudFrontDistributionId = frontendDistribution.id;
export const sqsDlqAlarmArn = dlqAlarm.arn;
export const cloudFrontLogsBucket = cfLogsBucket.bucket;
export {
  webSocketApiId,
  webSocketUrl,
  webSocketConnectionsTableName,
  webSocketEndpoint,
  eksClusterName,
  eksKubeconfig,
  eksWsiWorkerRoleArn,
};
