#!/usr/bin/python
import fileinput
import sys

lines = []
for line in fileinput.input():
	lines.append(line)

for line in reversed(lines):
	sys.stdout.write(line)
