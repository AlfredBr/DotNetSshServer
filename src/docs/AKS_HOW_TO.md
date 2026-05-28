# Running In AKS

This guide covers the host key setup needed when running the SSH server inside
Azure Kubernetes Service (AKS).

## Why The Host Key Path Matters

By default, the library uses this host key path:

```text
hostkey_ecdsa_nistp256.pem
```

Relative paths are resolved from the application's current working directory.
In many container images that is `/app`, so the default path becomes:

```text
/app/hostkey_ecdsa_nistp256.pem
```

If the file does not already exist, the library generates it and writes it to
disk. In AKS, `/app` is often not writable by the running container user, or the
pod may use a read-only root filesystem. In that case startup can fail with:

```text
System.UnauthorizedAccessException: Access to the path '/app/hostkey_ecdsa_nistp256.pem' is denied.
```

For production, provide a stable host key ahead of time and mount it into the
pod as a secret.

## Generate The Host Key

The default host key algorithm is ECDSA P-256:

```text
ecdsa-sha2-nistp256
```

Generate a compatible PEM file from a secure machine or secret-generation step:

```bash
openssl genpkey \
  -algorithm EC \
  -pkeyopt ec_paramgen_curve:P-256 \
  -out hostkey_ecdsa_nistp256.pem
```

Check that the file is a PKCS#8 private key:

```bash
head -1 hostkey_ecdsa_nistp256.pem
```

The output should be:

```text
-----BEGIN PRIVATE KEY-----
```

!!! Do not commit this file to source control !!!

## Create A Kubernetes Secret

Create a Kubernetes Secret from the PEM file:

```bash
kubectl create secret generic ssh-server-hostkey \
  --from-file=hostkey_ecdsa_nistp256.pem=./hostkey_ecdsa_nistp256.pem \
  -n dev
```

Replace `dev` with the namespace where the application runs.

## Mount The Secret

Mount the secret into the application pod and configure the SSH server to read
from the mounted file:

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: external-chatbot
  namespace: dev
spec:
  template:
    spec:
      volumes:
        - name: ssh-server-hostkey
          secret:
            secretName: ssh-server-hostkey
            defaultMode: 0444

      containers:
        - name: external-chatbot
          volumeMounts:
            - name: ssh-server-hostkey
              mountPath: /etc/ssh-server
              readOnly: true

          env:
            - name: SshServer__HostKeyPath
              value: /etc/ssh-server/hostkey_ecdsa_nistp256.pem
```

The environment variable overrides this appsettings value:

```json
{
  "SshServer": {
    "HostKeyPath": "/etc/ssh-server/hostkey_ecdsa_nistp256.pem"
  }
}
```

## Verify The Mounted Key

After redeploying, verify that the key is available inside the pod:

```bash
kubectl exec -n dev deploy/external-chatbot -- \
  ls -l /etc/ssh-server/hostkey_ecdsa_nistp256.pem
```

Optionally check the PEM header:

```bash
kubectl exec -n dev deploy/external-chatbot -- \
  sh -c 'head -1 /etc/ssh-server/hostkey_ecdsa_nistp256.pem'
```

The output should be:

```text
-----BEGIN PRIVATE KEY-----
```

## Multiple Replicas

If multiple pod replicas serve the same SSH endpoint, they should use the same
host key. Otherwise clients may connect to different pods and see host key
mismatch warnings.

A single Kubernetes Secret mounted into all replicas keeps the SSH server
identity stable across restarts and rescheduling.

## Alternative: Bake The Key Into The Image

You can place the PEM file in `/app` during image creation:

```dockerfile
COPY hostkey_ecdsa_nistp256.pem /app/hostkey_ecdsa_nistp256.pem
RUN chmod 600 /app/hostkey_ecdsa_nistp256.pem
```

If the application runs as a non-root user, make sure that user can read the
file:

```dockerfile
RUN chown app:app /app/hostkey_ecdsa_nistp256.pem
```

This works mechanically, but it is usually less flexible than mounting a secret.
Anyone with access to the image can extract the private host key, and rotating
the key requires rebuilding and redeploying the image.

For production AKS deployments, prefer a Kubernetes Secret or Azure Key Vault
with the Secrets Store CSI Driver.
