#!/bin/bash
# Run from project root.

set -e

kubectl delete secret termysecrets

kubectl create secret generic termysecrets --from-file=.hidden/azlogin --from-file=.hidden/cert.pfx --from-file=.hidden/certpw --from-file=.hidden/kubeconfig  --from-file=.hidden/adminpw