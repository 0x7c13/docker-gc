pushd `dirname $0` > /dev/null

cd ..
if [ ! -d venv ]; then
  mkdir venv
fi

cd venv
pip install virtualenv
virtualenv -p /usr/bin/python3 .
source bin/activate

cd ..
ROOT_PATH=`pwd`
popd > /dev/null

if [[ $1 == "noinstall" ]]; then
   echo "noinstall as first param so skipping install"
   export MSFTKUBE_SKIP_INSTALL=1
   shift
fi

mkver=`cat $ROOT_PATH/scripts/mkver`

if [ -z "$MSFTKUBE_SKIP_INSTALL" ]; then
    if $(pip freeze | grep -q "-e git.*msftkube") ; then
        echo "Detected msftkube installed from local repository"
    elif $(pip freeze | grep -q "msftkube==$mkver") ; then
        echo "Already on correct version of msftkube"
    else
        echo "Installing msftkube"
        url=https://msftkube.blob.core.windows.net/public/msftkube-$mkver-py3-none-any.whl
        pip install $url
    fi
else
    echo "Not installing msftkube because MSFTKUBE_SKIP_INSTALL is set"
fi  

python -u $ROOT_PATH/scripts/task.py "$@"
EXITCODE=$?

deactivate
exit $EXITCODE
