# feat-006: Multi-instance discovery on the same LAN

Status: backlog
Source: docs/product-roadmap.md (Possible Future Lanes)

## Description

Evaluate support for multiple LAN Portal instances running simultaneously on the same network,
each with a friendly name, discoverable and switchable by client users.

## Notes

- Real new work is discovery, not URL negotiation: evaluate mDNS/DNS-SD (e.g. `Makaretu.Dns`).
- Add a friendly-name setting per instance and a small API endpoint exposing local + discovered siblings.
- Add a client-side switcher between discovered instances.
- Known risk: mDNS is unreliable with AP/client isolation or per-device VLANs; first multicast
  broadcast will likely trigger a Windows Firewall prompt.
- Validate on real hardware across a few different router setups before considering this reliable enough to ship.
