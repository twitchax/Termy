ARG source=.


# Create a special builder image with build dependencies.
FROM microsoft/dotnet as builderimg
RUN apt-get update && apt-get install apt-transport-https
RUN curl -sS https://dl.yarnpkg.com/debian/pubkey.gpg | apt-key add -
RUN echo "deb https://dl.yarnpkg.com/debian/ stable main" | tee /etc/apt/sources.list.d/yarn.list
RUN apt-get update && apt-get install -y yarn
RUN curl -sL https://deb.nodesource.com/setup_9.x | bash -
RUN apt-get install -y nodejs


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

# Get Azure CLI.
RUN apt-get update -qq && apt-get install -qqy python libssl-dev libffi-dev python-dev build-essential
RUN curl -L https://azurecliprod.blob.core.windows.net/install.py > azcliinstall.py
RUN chmod a+x azcliinstall.py
RUN echo -ne "\n\n" | ./azcliinstall.py


# Run the yarn install separately since it doesn't change often.
FROM builderimg as yarnbuilder
ARG source
WORKDIR /builder
COPY ${source}/src/Core/package.json .
COPY ${source}/src/Core/yarn.lock .
RUN yarn install

# Run the termy build in a builder.
FROM builderimg as builder
ARG source
ARG config="Release"
WORKDIR /builder
COPY ${source}/src/Core .
COPY --from=yarnbuilder /builder/node_modules node_modules
RUN if [ "${config}" = "Release" ]; then yarn build; else yarn builddebug; fi

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
COPY ${source}/assets/terminal.yml .
COPY ${source}/assets/start-host.sh .

ENTRYPOINT dotnet Termy.dll