#!/bin/bash
# Run from project root.

set -e

kubectl delete secret termysecrets --namespace default
kubectl delete secret termysecrets --namespace terminals

kubectl create secret generic termysecrets --namespace default --from-file=.hidden/azlogin --from-file=.hidden/cert.pfx --from-file=.hidden/certpw --from-file=.hidden/kubeconfig  --from-file=.hidden/adminpw
kubectl create secret generic termysecrets --namespace terminals --from-file=.hidden/azlogin --from-file=.hidden/cert.pfx --from-file=.hidden/certpw --from-file=.hidden/kubeconfig  --from-file=.hidden/adminpw