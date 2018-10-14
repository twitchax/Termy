ARG source=.


# Create a special builder image with build dependencies.
FROM microsoft/dotnet as builderimg
RUN apt-get update && apt-get install apt-transport-https
RUN curl -sL https://deb.nodesource.com/setup_10.x | bash -
RUN apt-get install -y nodejs
RUN npm install -g npm


# Create a special host builder img.
FROM ubuntu as hostbuilderimg
RUN apt-get update && apt-get install -y wget gnupg curl && curl -sL https://deb.nodesource.com/setup_9.x | bash - && apt-get install -y nodejs && apt-get install -y build-essential


# Create a special shippable image with ship dependencies.
FROM microsoft/dotnet:aspnetcore-runtime as shipimg

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

# Run the termy build in a builder.
FROM builderimg as builder
ARG source
ARG config="Release"
WORKDIR /builder

COPY --from=packagebuilder /builder/node_modules node_modules
COPY ${source}/src/Core/package.json .
COPY ${source}/src/Core/package-lock.json .
COPY ${source}/src/Core/tsconfig.json .

COPY ${source}/src/Core/Client ./Client
COPY ${source}/src/Core/index.html .
RUN npm run buildclient

COPY ${source}/src/Core/Termy.csproj .
RUN dotnet restore

COPY ${source}/src/Core .
RUN dotnet publish -c ${config}

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
COPY --from=builder /builder/bin/${config}/netcoreapp2.1/publish .
COPY --from=hostbuilder /builder/termy-terminal-host ./assets/
COPY --from=hostbuilder /builder/node_modules/node-pty/build/Release/pty.node ./assets/

ENTRYPOINT dotnet Termy.dll