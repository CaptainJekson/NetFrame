#!/bin/bash
# source path
source_folder="/Users/evgeniyskvortsov/UnityProjects/NetFrame/Packages/com.jeskvo.net-frame/NetFrame"

# destination path
destination_folder="/Users/evgeniyskvortsov/UnityProjects/NetFrame_2/Packages/com.jeskvo.net-frame/NetFrame"

# copy
/usr/bin/rsync -avz --exclude='.git*' --delete "$source_folder/" "$destination_folder/"

# source path
source_folder_assets="/Users/evgeniyskvortsov/UnityProjects/NetFrame/Assets"

# destination path
destination_folder_assets="/Users/evgeniyskvortsov/UnityProjects/NetFrame_2/Assets"

# copy
/usr/bin/rsync -avz --exclude='.git*' --delete "$source_folder_assets/" "$destination_folder_assets/"