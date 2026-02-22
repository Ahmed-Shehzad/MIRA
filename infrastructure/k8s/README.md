# Kubernetes Manifests (EKS)

When `enableEksGpu=true`, deploy the WSI worker to EKS:

```bash
# 1. Get Pulumi outputs
export ECR_WORKER_IMAGE=$(pulumi stack output ecrWorkerRepositoryUrl):latest
export AWS_REGION=us-east-1
export S3_BUCKET=$(pulumi stack output uploadsBucketName)

# 2. Configure kubectl
aws eks update-kubeconfig --name $(pulumi stack output eksClusterName) --region $AWS_REGION

# 3. (Optional) Annotate service account for IRSA (pod-level IAM)
kubectl annotate serviceaccount wsi-worker -n mira eks.amazonaws.com/role-arn=$(pulumi stack output eksWsiWorkerRoleArn) --overwrite

# 4. Apply manifests (node role has S3/SQS; envsubst replaces placeholders)
envsubst < wsi-worker-deployment.yaml | kubectl apply -f -
```

**IRSA:** When using the annotation above, pods use the `eksWsiWorkerRoleArn` IAM role instead of the node role for S3/SQS. This provides stricter pod-level IAM.

For GPU workloads, add `nodeSelector` for GPU nodes and increase resources. See [WSI-GPU-WORKER.md](../../docs/WSI-GPU-WORKER.md).
