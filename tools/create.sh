#!/bin/bash
# Run from project root.

set -e

kubectl create -f assets/termy.yml

kubectl expose deployment termy --type=LoadBalancer --name=termy