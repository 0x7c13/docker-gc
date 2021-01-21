#!/bin/bash
set -ex
cd `dirname $0` > /dev/null
cd ..

python -m venv /tmp/aml-ve
source /tmp/aml-ve/bin/activate

wget -O - https://aka.ms/msftkube-bootstrapper.sh | bash
msftkube "$@"
