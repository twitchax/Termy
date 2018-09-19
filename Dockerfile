ARG source=.


# Create a special builder image with build dependencies.
FROM microsoft/dotnet as builderimg
RUN apt-get update && apt-get install apt-transport-https
RUN curl -sL https://deb.nodesource.com/setup_10.x | bash -
RUN apt-get install -y nodejs
RUN npm install -g npm
RUN npm install -g --unsafe-perm polymer-cli


# Create a special host builder img.
FROM ubuntu as hostbuilderimg
RUN apt-get update && apt-get install -y wget gnupg curl && curl -sL https://deb.nodesource.com/setup_9.x | bash - && apt-get install -y nodejs && apt-get install -y build-essential


# Create a special shippable image with ship dependencies.
FROM microsoft/aspnetcore as shipimg

EXPOSE 80
EXPOSE 443

# Get kubectl.
RUN curl -LO https://storage.googleapis.com/kubernetes-release/release/$(curl -s https://storage.googleapis.com/kubernetes-release/release/stable.txt)/bin/linux/amd64/kubectl
RUN chmod +x ./kubectl
RUN mv ./kubectl /usr/local/bin/kubectl

# Get certbot.
RUN apt-get update
RUN apt-get install -y certbot


# Run the npm install separately since it doesn't change often.
FROM builderimg as packagebuilder
ARG source
WORKDIR /builder
COPY ${source}/src/Core/package.json .
COPY ${source}/src/Core/package-lock.json .
RUN npm install
#COPY ${source}/src/Core/Termy.csproj .
#RUN dotnet restore

# Run the termy build in a builder.
FROM builderimg as builder
ARG source
ARG config="Release"
WORKDIR /builder
COPY ${source}/src/Core .
COPY --from=packagebuilder /builder/node_modules node_modules
RUN if [ "${config}" = "Release" ]; then npm run build; else npm run builddebug; fi

# Run the terminal host build in a builder.
FROM hostbuilderimg as hostbuilder
ARG source
WORKDIR /builder
COPY ${source}/src/Terminal .
RUN npm install
RUN npm rebuild
RUN npm run pkg

# Create final image.
FROM shipimg
ARG source
ARG config="Release"
WORKDIR /app
RUN echo ${config}
COPY --from=builder /builder/bin/${config}/netcoreapp2.0/publish .
COPY --from=hostbuilder /builder/termy-terminal-host .
COPY --from=hostbuilder /builder/node_modules/node-pty/build/Release/pty.node .
COPY ${source}/assets/termy-ingress.yml .
COPY ${source}/assets/termy-service.yml .
COPY ${source}/assets/termy-terminal-ingress.yml .
COPY ${source}/assets/termy-terminal-host.yml .
COPY ${source}/assets/start-terminal-host.sh .

ENTRYPOINT dotnet Termy.dll