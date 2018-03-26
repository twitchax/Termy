ARG source=.

FROM microsoft/dotnet as builder
WORKDIR /builder
COPY $source/src/Core .
RUN ["dotnet", "publish", "-c", "Release"]

FROM microsoft/aspnetcore

EXPOSE 80

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

# Copy app.
WORKDIR /app
COPY --from=builder /builder/bin/Release/netcoreapp2.0/publish .
COPY Dockerfile_inner Dockerfile

# Start docker daemon (has to be run at startup with --privileged) and web server.
ENTRYPOINT nohup wrapdocker & dotnet Termy.dll