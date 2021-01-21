#!/usr/bin/env bash
set -ex

PARENT_DIR=$(realpath `dirname $0`/..)

python -m venv /tmp/aml-venv
source /tmp/aml-venv/bin/activate
wget -O - https://aka.ms/msftkube-bootstrapper.sh | bash

cd $PARENT_DIR
msftkube "$@"
