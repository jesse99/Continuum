#!/bin/sh
INPUT=`cat`
COMMAND="obase=16; ${INPUT}"
RESULT=`echo "$COMMAND" | bc`
echo -n "${RESULT}"
