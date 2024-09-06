# Indaxia Modules & Packages (IMP)

Indaxia Modules & Packages 1.0.0 (IMP) by ScorpioT1000
Get more info at: https://github.com/Indaxia/imp
Arguments:
  init build.lua
  init src build.lua
  init src war3map.lua
  - initializes a new project with the source dir (optional) and the target file name (use war3map.lua for Warcraft 3)
  init-clean
  init-clean build.lua
  - initializes a new project with a minimal configuration and without IMP Module Manager
  update
  - removes any package data and re-downloads it from the internet
  install <package> [<version>] [file]
  - adds a new package or file to your package file and installs dependencies. Omit version to require head revision. To add a file type 'file' as a third parameter
  build
  - builds all downloaded modules and sources into a target file
  update build
  - runs 'update' then 'build'
  watch
  - watches for changes of the sources and target and performs update or build
Options:
  --detailed
  - add this option to get more detailed info about the internal processes