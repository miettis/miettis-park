"""Validate SemVer before publishing and derive Docker-compatible GHCR tags."""
import os
import re

tag = os.environ["GITHUB_REF_NAME"]
number = r"(?:0|[1-9][0-9]*)"
identifier = r"(?:0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*)"
pattern = rf"v({number}\.{number}\.{number})(?:-({identifier}(?:\.{identifier})*))?(?:\+([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?"
match = re.fullmatch(pattern, tag)
if not match:
    raise SystemExit(f"Invalid semantic version tag: {tag}")
version = tag[1:].replace("+", "_")
if len(version) > 128:
    raise SystemExit("Version exceeds Docker tag length limit")
image = "ghcr.io/" + os.environ["GITHUB_REPOSITORY"].lower()
tags = [f"{image}:{version}", f"{image}:sha-{os.environ['GITHUB_SHA'][:12]}"]
if match.group(2) is None and match.group(3) is None:
    tags.append(f"{image}:latest")
with open(os.environ["GITHUB_OUTPUT"], "a", encoding="utf-8") as output:
    output.write("tags<<TAGS\n" + "\n".join(tags) + "\nTAGS\n")
