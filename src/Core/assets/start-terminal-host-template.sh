#!/bin/bash

set -e

# Get files from termy server.
curl {{termyhostname}}/assets/termy-terminal-host -o /terminal-host/termy-terminal-host
curl {{termyhostname}}/assets/pty.node -o /terminal-host/pty.node

# Update superuser password.
echo root:$(cat /etc/secrets/supw) | chpasswd

# Create `guest` account.
useradd -m -s $TERMY_SHELL guest
if [ $TERMY_SHELL -ne "null" ]; then
    echo guest:$TERMY_PASSWORD | chpasswd
fi

# Install openssh server.
# apt-get update && apt-get install -y openssh-server || true

# Run terminal host server.
chmod a+x /terminal-host/termy-terminal-host
nohup /terminal-host/termy-terminal-host > /terminal-host/nohup.std 2> /terminal-host/nohup.err &