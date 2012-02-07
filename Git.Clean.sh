#!/bin/sh
git clean -d -x -f -e TODO.txt -e Distribution/.git -n
read -p "Check changes, then press [ENTER] to execute."
git clean -d -x -f -e TODO.txt -e Distribution/.git
read -p "Press [ENTER] to quit."