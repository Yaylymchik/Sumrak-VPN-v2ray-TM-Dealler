#!/bin/bash

set_proxy() {
    PROXY_IP=$1
    PROXY_PORT=$2
    shift 2
    BYPASS_DOMAINS=("$@")
    if [ ${#BYPASS_DOMAINS[@]} -eq 0 ]; then
        BYPASS_DOMAINS=("")
    fi

    SERVICES=$(networksetup -listallnetworkservices | grep -v '*')
    echo "$SERVICES" | while read -r SERVICE; do
        networksetup -setwebproxy "$SERVICE" "$PROXY_IP" "$PROXY_PORT"
        networksetup -setsecurewebproxy "$SERVICE" "$PROXY_IP" "$PROXY_PORT"
        networksetup -setsocksfirewallproxy "$SERVICE" "$PROXY_IP" "$PROXY_PORT"
        networksetup -setproxybypassdomains "$SERVICE" "${BYPASS_DOMAINS[@]}"
    done
}

clear_proxy() {
    SERVICES=$(networksetup -listallnetworkservices | grep -v '*')
    echo "$SERVICES" | while read -r SERVICE; do
        networksetup -setwebproxystate "$SERVICE" off
        networksetup -setsecurewebproxystate "$SERVICE" off
        networksetup -setsocksfirewallproxystate "$SERVICE" off
    done
}

if [ "$1" == "set" ]; then
    if [ "$#" -lt 3 ]; then
        echo "Usage: $0 set <IP Address> <Port> [Bypass Domain 1 ...]"
        exit 1
    fi
    set_proxy "$2" "$3" "${@:4}"
elif [ "$1" == "clear" ]; then
    clear_proxy
else
    echo "Usage:"
    echo "  $0 set <IP Address> <Port> [Bypass Domain 1 ...]"
    echo "  $0 clear"
    exit 1
fi
