#!/bin/bash

set -e

kubectl create -f termy.yml

kubectl expose deployment termy --type=LoadBalancer --name=termy