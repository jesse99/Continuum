#!/bin/sh
UPPER=`tr "[:lower:]" "[:upper:]"`
COMMAND="ibase=16; $UPPER"
RESULT=`echo "$COMMAND" | bc`
echo -n "${RESULT}"
