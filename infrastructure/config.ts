import * as pulumi from "@pulumi/pulumi";

const config = new pulumi.Config();

export interface StackConfig {
  environment: string;
  awsRegion: string;
  projectName: string;
  dbInstanceClass: string;
  dbAllocatedStorage: number;
  ecsCpu: string;
  ecsMemory: string;
  desiredCount: number;
  domainName?: string;
  apiSubdomain?: string;
  hostedZoneId?: string;
  acmCertificateArn?: string;
  enableCognitoHostedUi: boolean;
  enableEksGpu: boolean;
  enableWebSocketApi: boolean;
  enableWsiWorker: boolean;
  enableXRayTracing: boolean;
  frontendSubdomain?: string;
  apiScalingMin?: number;
  apiScalingMax?: number;
  workerScalingMin?: number;
  workerScalingMax?: number;
}

export function getConfig(): StackConfig {
  const env = config.require("environment");
  return {
    environment: env,
    awsRegion: config.get("awsRegion") ?? "us-east-1",
    projectName: config.get("projectName") ?? "mira",
    dbInstanceClass: config.get("dbInstanceClass") ?? "db.t3.micro",
    dbAllocatedStorage: config.getNumber("dbAllocatedStorage") ?? 20,
    ecsCpu: config.get("ecsCpu") ?? "256",
    ecsMemory: config.get("ecsMemory") ?? "512",
    desiredCount: config.getNumber("desiredCount") ?? 1,
    domainName: config.get("domainName"),
    apiSubdomain: config.get("apiSubdomain") ?? "api",
    hostedZoneId: config.get("hostedZoneId"),
    acmCertificateArn: config.get("acmCertificateArn"),
    enableCognitoHostedUi: config.getBoolean("enableCognitoHostedUi") ?? true,
    enableEksGpu: config.getBoolean("enableEksGpu") ?? false,
    enableWebSocketApi: config.getBoolean("enableWebSocketApi") ?? false,
    enableWsiWorker: config.getBoolean("enableWsiWorker") ?? true,
    enableXRayTracing: config.getBoolean("enableXRayTracing") ?? false,
    frontendSubdomain: config.get("frontendSubdomain"),
    apiScalingMin: config.getNumber("apiScalingMin"),
    apiScalingMax: config.getNumber("apiScalingMax"),
    workerScalingMin: config.getNumber("workerScalingMin"),
    workerScalingMax: config.getNumber("workerScalingMax"),
  };
}
