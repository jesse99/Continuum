#!/bin/bash
# Usage: ./get_version.sh 0.3.xxx.0 build_num
# where build_num is a file with a single number.
BASE_VERSION="$1"
FILE="$2"

if [ -f "$FILE" ]
then
	BUILD_NUM=`cat "$FILE"`
	((BUILD_NUM = $BUILD_NUM + 1))
else
	BUILD_NUM=0
fi

echo $BUILD_NUM > "$FILE"
echo $BASE_VERSION | sed "s/xxx/$BUILD_NUM/"
