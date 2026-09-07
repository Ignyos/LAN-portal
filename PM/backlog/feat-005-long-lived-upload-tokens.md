# feat-005: Long-lived upload tokens for long transfers

Status: backlog
Source: docs/product-roadmap.md (Possible Future Lanes)

## Description

Keep access tokens valid for the life of a long transfer. Options to weigh: refresh the token
mid-batch, issue a scoped short-lived upload token per file, or treat it as solved by chunked
uploads (feat-004). No action for now; revisit alongside chunked, resumable transfers.
