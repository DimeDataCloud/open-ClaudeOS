# Security

open-ClaudeOS lets a language model propose actions on a person's computer, so
its security model is the product. The design is in the
[threat model](docs/threat-model.md); this page is how to report problems and
what is in scope.

## Reporting a vulnerability

Please report privately through GitHub's
["Report a vulnerability"](../../security/advisories/new) on this repository,
not in a public issue. Include what you did, what you expected, and what
happened. You will get an acknowledgement; there is no bounty.

## What counts

The most serious class is any way for **text the model reads** (a document, a
web page, a filename, a mod, a tool result) to cause an action the person did
not approve. Examples that are in scope:

- a plan that runs, or reaches the approval card with a different meaning than
  it will execute with (the card must be generated from the typed actions, and a
  grant is bound to their digests);
- bypassing policy: protected paths (`.ssh`, `.env`, keys, credential stores),
  path escapes (symlinks, `..`, reserved device names, alternate data streams,
  case tricks), or external actions that do not need approval;
- an undo that overwrites something the person changed afterwards;
- a mod reading data it was not approved for, or a manifest edit that keeps an
  earlier approval;
- the API key leaving the credential store (log, file, crash report, audit log);
- anything that makes the audit log lie about what was done.

Out of scope: needing local admin or malware already running as the user;
denial of service by the person against their own machine; findings that depend
on disabling a protection in the app's settings.

## What the design promises, and what it does not

- Policy, consent, placement, routing and undo are deterministic code, not
  prompts. No instruction in any file can change them.
- The Claude API key is stored in the Windows per-user credential store, never in
  the repository, a settings file or the audit log.
- External actions (email, HTTP) are shown in full and always need approval; the
  hold-to-approve control makes a stray keypress insufficient.
- Mods are declarative by default: no code runs. Scripted and web mods are
  refused until their sandboxes exist.

It does **not** promise: that the model's plans are good (it shows them to you
so you can judge), safety against a malicious *approved* action, or protection
of anything you explicitly approve. The Windows shell has not yet had an
independent security review; treat it as a pre-release.
