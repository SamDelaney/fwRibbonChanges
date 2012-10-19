#!/bin/bash
# Finish a feature branch
# If no feature name is given as parameter the name of the current feature branch will be used.

developConfig=$(git config --get gitflow.branch.develop)
featureConfig=$(git config --get gitflow.prefix.feature)
originConfig=$(git config --get gitflow.origin)
develop=${developConfig:-develop}
feature=${featureConfig:-feature/}
origin=${originConfig:-origin}

if [ -z $1 ]; then
	branch=$(git symbolic-ref -q HEAD || git name-rev --name-only HEAD 2>/dev/null)
	if [ "$branch" == "${branch#refs/heads/$feature}" ]; then
		echo "ERROR: Either specify the feature to close or switch to a feature branch"
		exit 1
	fi
	branch=${branch#refs/heads/$feature}
fi

feat=${1:-$branch}

set -e

git fetch $origin
git checkout $feature$feat
git rebase $origin/$develop
git checkout $develop
git pull --rebase $origin $develop
git branch -d $feature$feat
