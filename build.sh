#!/bin/sh
dir=`dirname $0`
pwsh "$dir/build.ps1" $*
