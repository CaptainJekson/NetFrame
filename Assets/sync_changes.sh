#!/bin/bash
# source path
source_folder="/Users/evgeniyskvortsov/UnityProjects/NetFrame/Packages/com.jeskvo.net-frame/NetFrame"

# destination path
destination_folder="/Users/evgeniyskvortsov/UnityProjects/NetFrame.Net/NetFrame.Net/NetFrame"

# copy
/usr/bin/rsync -avz --exclude='.git*' --exclude='*.meta' --exclude="*.asmdef" --delete "$source_folder/" "$destination_folder/"
