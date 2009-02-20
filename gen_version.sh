#!/bin/bash
# Usage: ./gen_version.sh 1.3.57.0 source/internal/AssemblyVersion.cs
VERSION="$1"
FILE="$2"

echo "// Machine generated: do not manually edit." > ${FILE}
echo "using System.Reflection;" >> ${FILE}
echo " " >> ${FILE}
echo "[assembly: AssemblyVersion(\"${VERSION}\")]" >> ${FILE}
touch -t 0012221500 ${FILE}		# don't regenerate assemblies if only the build number changed
