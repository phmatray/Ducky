#!/bin/bash

# Exit immediately if a command exits with a non-zero status.
set -e

# Check for markdown errors
echo "Checking markdown syntax"
markdownlint docs/*.md || { echo "*** ERROR *** An error occurred. Website was not generated."; exit 1; }

# Check the docs folder for references and attachments
#echo "Checking references and attachments"
#doclinkchecker -d ./docs -a || { echo "*** ERROR *** An error occurred. Website was not generated."; exit 1; }

# Generate the table of contents for General
echo "Generating table of contents for General"
docfxtocgenerator -d ./docs/general -sri || { echo "*** ERROR *** An error occurred. Website was not generated."; exit 1; }

# Generate the table of contents for Services
echo "Generating table of contents for Services"
docfxtocgenerator -d ./docs/services -sri || { echo "*** ERROR *** An error occurred. Website was not generated."; exit 1; }

# Clean up old generated files
echo "Cleaning up previous generated contents"
rm -rf docs/_site
rm -rf docs/_pdf
rm -rf docs/reference

# Generate the website
echo "Generating website in _site"
docfx ./docs/docfx.json $1 || { echo "*** ERROR *** An error occurred. Website was not generated."; exit 1; }

echo "Website generated successfully."
