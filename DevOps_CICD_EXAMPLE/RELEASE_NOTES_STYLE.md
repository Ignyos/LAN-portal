# Release Notes Style Guide

This document defines the style and format for Playlist release notes.

## Overall Structure

Use this as the primary template. Emoji section headers are fine (see example below).

```markdown
## Overview
Brief summary of what this release focuses on (1-2 sentences).

## New Features
- **Feature Name**: Description of the feature and user benefit
- **Another Feature**: What it does and why it matters

## Improvements
- **Area/Component**: What was improved and the impact
- **UI Enhancement**: Visual or UX improvements made

## Bug Fixes
- **Issue Description**: What was fixed and the symptom it resolves
- **Another Fix**: The problem and solution

## Technical Changes
- Infrastructure improvements
- Performance optimizations
- Dependency updates

## Breaking Changes (if any)
- Clear description of what changed and migration steps

## Installation
- How to install / run

## Requirements
- OS and runtime prerequisites

## Documentation
- Links to docs
```

## Writing Guidelines

### Tone & Style
- **User-focused**: Write for end-users, not developers (unless it's a technical note)
- **Clear & concise**: Use simple language, avoid jargon
- **Action-oriented**: Start with verbs (Added, Fixed, Improved, Updated)
- **Present tense**: "Adds support for..." not "Added support for..."

### Formatting
- Use **bold** for feature names or components
- Bullets: use `-` consistently (emoji headers are OK); keep each bullet to one clear sentence
- Group related changes together and order by importance
- Optional emoji section headers like `## 🎉 What's New` are welcome if they improve readability

### Content Focus
- **What changed**: The feature or fix
- **Why it matters**: User benefit or impact
- **How to use** (if needed): Brief usage hint for new features

### Example Transformations

**From raw diff:**
```
+++ b/docs/Playlist/MainWindow.xaml
Added Settings_Click handler
```

**To release note:**
```
- **Settings Menu**: Implemented settings dialog functionality - clicking Settings now opens the configuration panel
```

**From raw diff:**
```
Modified progress indicator styling
Background color changes based on completion
```

**To release note:**
```
- **Progress Indicators**: Completed items now display with a green background for better visual feedback
```

## Sections to Always Include

1. **Overview** - Even if brief
2. **New Features** - User-visible additions
3. **Improvements** - Enhancements to existing features
4. **Bug Fixes** - Issues resolved
5. **Technical Changes** - Behind-the-scenes updates (optional)
6. **Installation** - How to get the build
7. **Requirements** - OS/runtime dependencies
8. **Documentation** - Where to read more

## What to Omit

- Internal refactoring with no user impact
- Whitespace-only changes
- Trivial code style updates
- Test file changes (unless test coverage is notable)
- Build script changes (unless they affect users)

## Version Header Format

Always start with:
```markdown
# Release v{VERSION}
```

Example: `# Release v1.2.1`

## Final Checklist

- [ ] Version number in header
- [ ] Overview explains the "why" of this release
- [ ] Features are user-focused and benefit-driven
- [ ] Technical jargon is minimized
- [ ] Each section has at least one item (or is omitted)
- [ ] Changes are ordered by importance
- [ ] Breaking changes are clearly highlighted
- [ ] Grammar and spelling are correct

## Full Example (previous release)

```markdown
## 🎉 What's New

### Settings & Startup Control
- New Settings window: manage Run on Windows Startup and Media Player fullscreen behavior (Auto vs Default).
- Startup preference is applied when the app launches and can be toggled from Settings.

### Video Progress Behavior
- Progress stays at 100% when a video completes (timestamp saved at full duration).
- Double-clicking a completed item starts it from the beginning; partial progress resumes from the saved timestamp.
- Stop button now saves progress before closing the player.

### Drag-to-Sort Reordering
- More reliable and intuitive reordering with a full-width insertion indicator.
- Index-based targeting ensures consistent drag behavior, including dropping at the end of the list.
- Fixes the out-of-bounds error seen when dropping near list boundaries.

### UI/UX Improvements
- Progress Indicator Pill turns pale green at 100% for easy recognition.
- Increased progress pill font size for readability; consistent black text.
- Playlists list now has a clear hover highlight that matches playlist items.
- Restored column headers (Progress, Title) for a consistent layout.
- Hidden grid splitter width for a cleaner two-column interface.

## 📦 Installation
- Download and run `PlaylistSetup.exe`

## 📋 Requirements
- Windows 10/11 (64-bit)
- .NET 9.0 Runtime (included in installer)

## 📖 Documentation
- Full documentation available at https://playlist.ignyos.com/
```
