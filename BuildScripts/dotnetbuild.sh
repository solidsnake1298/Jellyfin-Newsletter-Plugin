#!/bin/bash
. venv/bin/activate
cd /Development

if [[ "${1}" == "prod" ]]; then
    export JELLYFIN_REPO="./Jellyfin.Plugin.NewslettersRedux"
    export JELLYFIN_REPO_URL="https://github.com/thedreaddpirate/Jellyfin-Newsletter-Plugin/releases/download"
    ./BuildScripts/jprm_build.sh
    cp ./Jellyfin.Plugin.NewslettersRedux/manifest.json ./manifest.json
else
    dotnet build
fi
exit $?
