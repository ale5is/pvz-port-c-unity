# Horizon build gateway

This service keeps the GitHub token off the Android device. The app POSTs a framed .hnmod request;
the gateway validates it, stores it temporarily, dispatches the repository workflow, polls Actions,
checks the artifact SHA-256, and serves the compiled ARM64 library.

## Run

Build the Docker image from the repository root:

    docker build -f native-build/gateway/Dockerfile -t horizon-mod-build .

Run it behind an HTTPS reverse proxy. Set:
- GITHUB_TOKEN: fine-grained token with Actions read/write for ale5is/pvz-port-c-unity.
- GITHUB_REPOSITORY: repository containing horizon-mod-build.yml.
- GITHUB_REF: branch containing that workflow.
- PUBLIC_BASE_URL: externally reachable HTTPS base URL, no trailing slash.
- DATA_DIR: persistent directory for temporary requests and compiled libraries.

Example:

    docker run -d --restart unless-stopped -p 127.0.0.1:8080:8080 \
      -v /var/lib/horizon-mod-build:/data \
      -e GITHUB_TOKEN=... \
      -e GITHUB_REPOSITORY=ale5is/pvz-port-c-unity \
      -e GITHUB_REF=port/horizon-078-hooks-359 \
      -e PUBLIC_BASE_URL=https://build.example.com \
      horizon-mod-build

Do not expose port 8080 directly to the internet. Terminate TLS at a reverse proxy, enable request
body limits and rate limiting there too, and keep the token only in the server's environment.

The default gateway rate limit is three requests per source IP per hour and three concurrent jobs.
The GitHub workflow runs untrusted native code only in the networkless sandbox; the gateway never
executes mod code itself. Native libraries still have full in-game process permissions.
