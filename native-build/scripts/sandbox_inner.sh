#!/usr/bin/env bash
set -euo pipefail
home=$1 root=$2 job=$3 uid=$4 gid=$5 base_build=$6 scripts=$7 java_home=$8
mount -t proc proc /proc
mount -t tmpfs -o mode=1777 tmpfs /tmp
keep=/mnt
mount -t tmpfs tmpfs "$keep"
place() { mkdir -p "$keep/$1"; mount --bind "$2" "$keep/$1"; }
place rustup "$home/.rustup"
place android "$home/android"
place java "$java_home"
place cargo "$root/cargo"
if [ "$base_build" = 1 ]; then
  place cache "$root/base"
else
  layer=$job/overlay
  mkdir -p "$layer/upper" "$layer/work" "$keep/cache"
  chown "$uid:$gid" "$layer/upper"
  mount -t overlay overlay -o "lowerdir=$root/base,upperdir=$layer/upper,workdir=$layer/work" "$keep/cache"
fi
place source "$root/source"
place mods "$job/mods"
place out "$job/out"
place builtin "$root/builtin"
place patches "$root/patches"
place scripts "$scripts"
mount -t tmpfs -o "mode=755,uid=$uid,gid=$gid" tmpfs "$home"
back() {
  mkdir -p "$home/$2"
  mount --move "$keep/$1" "$home/$2"
  [ "$3" = rw ] || mount -o remount,bind,ro "$home/$2"
}
back rustup .rustup ro
back android android ro
back java java ro
back cargo .cargo rw
back cache .cache/horizon-native rw
back source source ro
back mods job/mods ro
back out job/out rw
back builtin builtin ro
back patches patches ro
back scripts scripts ro
umount "$keep"
mkdir -p "$home/job"
chown "$uid:$gid" "$home" "$home/.cache" "$home/job"
cd /tmp
exec setpriv --reuid="$uid" --regid="$gid" --clear-groups --no-new-privs \
  --bounding-set=-all --inh-caps=-all \
  env -i HOME="$home" USER="$(id -nu "$uid")" PATH="$home/.cargo/bin:/usr/bin:/bin" CARGO_HOME="$home/.cargo" RUSTUP_HOME="$home/.rustup" \
  ANDROID_HOME="$home/android" JAVA_HOME="$home/java" NATIVE_CACHE="$home/.cache/horizon-native" \
  CARGO_NET_OFFLINE=true \
  bash -c '. "$HOME/.cargo/env"; exec bash "$HOME/scripts/native_build.sh" "$HOME/source" "$HOME/job/out" "$HOME/job/mods"'
