# Local development certificate

The original shared development private key has been removed from this repository and its published branch history. Generate a new local certificate before using `dev/docker-compose.yml`:

```sh
openssl req -x509 -newkey rsa:2048 -nodes -days 365 -keyout dev/certs/private.key -out dev/certs/public.crt -subj '/CN=localhost' -addext 'subjectAltName=DNS:localhost,DNS:minio,IP:127.0.0.1'
```

Keep the private key local. These certificates are for local development only.
