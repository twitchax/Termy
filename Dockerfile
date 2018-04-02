ARG source=.

# Create a special builder image with build dependencies.
FROM microsoft/dotnet as builderimg
RUN apt-get update && apt-get install apt-transport-https
RUN curl -sS https://dl.yarnpkg.com/debian/pubkey.gpg | apt-key add -
RUN echo "deb https://dl.yarnpkg.com/debian/ stable main" | tee /etc/apt/sources.list.d/yarn.list
RUN apt-get update && apt-get install -y yarn
RUN curl -sL https://deb.nodesource.com/setup_9.x | bash -
RUN apt-get install -y nodejs


# Create a special sippable image with ship dependencies.
FROM microsoft/aspnetcore as shipimg

EXPOSE 80
EXPOSE 443

# Get docker dependencies.
RUN apt-get update -qq && apt-get install -qqy \
    apt-transport-https \
    ca-certificates \
    curl \
    lxc \
    iptables
    
# Install docker.
RUN curl -sSL https://get.docker.com/ | sh

# Install the magic wrapper.
COPY ./wrapdocker /usr/local/bin/wrapdocker
RUN chmod +x /usr/local/bin/wrapdocker

# Define the docker volume mapping.
VOLUME /var/lib/docker

# Get kubectl.
RUN curl -LO https://storage.googleapis.com/kubernetes-release/release/$(curl -s https://storage.googleapis.com/kubernetes-release/release/stable.txt)/bin/linux/amd64/kubectl
RUN chmod +x ./kubectl
RUN mv ./kubectl /usr/local/bin/kubectl

# Get Azure CLI.
RUN apt-get update -qq && apt-get install -qqy python libssl-dev libffi-dev python-dev build-essential
RUN curl -L https://azurecliprod.blob.core.windows.net/install.py > azcliinstall.py
RUN chmod a+x azcliinstall.py
RUN echo -ne "\n\n" | ./azcliinstall.py

# Run the build in a builder.
FROM builderimg as builder
ARG source
ARG config="Release"
WORKDIR /builder
COPY ${source}/src/Core .
RUN yarn install
RUN if [ "${config}" = "Release" ]; then yarn build; else yarn builddebug; fi

# Create shippable image.
FROM shipimg
ARG config="Release"
WORKDIR /app
RUN echo ${config}
COPY --from=builder /builder/bin/${config}/netcoreapp2.0/publish .
COPY Dockerfile_inner Dockerfile

# Start docker daemon (has to be run at startup with --privileged) and web server.
ENTRYPOINT nohup wrapdocker & dotnet Termy.dll