# This is a test dockerfile.  The binary is normally mounted in Kubernetes.

FROM ubuntu as builderimg
RUN apt-get update && apt-get install -y wget gnupg curl && curl -sL https://deb.nodesource.com/setup_9.x | bash - && apt-get install -y nodejs && apt-get install -y build-essential

FROM builderimg as builder
WORKDIR /builder
COPY . .
RUN npm install
RUN npm rebuild
RUN npm run pkg

FROM ubuntu
WORKDIR /app
COPY --from=builder /builder/termy-terminal-host .
COPY --from=builder /builder/node_modules/node-pty/build/Release/pty.node .
ENTRYPOINT /app/termy-terminal-host