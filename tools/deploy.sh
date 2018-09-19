#!/bin/bash

# Run from project root.
# Do not run in any subsequent operations.

set -e

# Ensure ingress controller.
kubectl apply -f https://raw.githubusercontent.com/kubernetes/ingress-nginx/master/deploy/mandatory.yaml
kubectl apply -f https://raw.githubusercontent.com/kubernetes/ingress-nginx/master/deploy/provider/cloud-generic.yaml

# Create namespace.
kubectl apply -f assets/termy-ns.yml

# Create secrets.
kubectl create secret generic termy-secrets --namespace termy --from-file=.hidden/hostname --from-file=.hidden/kubeconfig --from-file=.hidden/supw --from-file=.hidden/tls.crt --from-file=.hidden/tls.key
kubectl create secret generic termy-secrets --namespace termy-terminals --from-file=.hidden/tls.crt --from-file=.hidden/tls.key

# Create termy.
kubectl apply -f assets/termy.yml