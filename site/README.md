# Download page

The page behind https://get-jester.vercel.app: plain static files, no build step.
It names no author and does not link to this repository.

- `index.html` is the page.
- `vercel.json` holds the download addresses, each a temporary redirect:
  - `/download/windows`: the newest release's `Jester.exe`
- `llms.txt` describes Jester for AI assistants.
- `skill/jester/` is the assistant skill, and `skill/jester.zip` is that folder zipped. Rebuild the zip after an edit, from this folder:

  ```
  python -c "import shutil; shutil.make_archive('skill/jester', 'zip', 'skill', 'jester')"
  ```

Vercel deploys this folder whenever a push to main changes it. `.vercelignore` keeps this README off the site.
