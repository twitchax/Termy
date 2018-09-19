#!/bin/bash
# Run from project root.

set -e

kubectl delete secret termy-secrets --namespace termy
kubectl delete secret termy-secrets --namespace termy-terminals

kubectl create secret generic termy-secrets --namespace termy --from-file=.hidden/hostname --from-file=.hidden/kubeconfig --from-file=.hidden/supw --from-file=.hidden/tls.crt --from-file=.hidden/tls.key
kubectl create secret generic termy-secrets --namespace termy-terminals --from-file=.hidden/tls.crt --from-file=.hidden/tls.key