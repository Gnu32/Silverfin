#!/bin/sh
git clean -d -x -f -e TODO.txt -n
read -p "Check changes, then press [ENTER] to execute."
git clean -d -x -f -e TODO.txt
read -p "Press [ENTER] to quit."