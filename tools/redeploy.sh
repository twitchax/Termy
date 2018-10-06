#!/bin/bash
# Run from project root.

kubectl delete deploy/termy --namespace=termy

set -e

docker build -t twitchax/termy .
docker push twitchax/termy

kubectl apply -f termy.yml