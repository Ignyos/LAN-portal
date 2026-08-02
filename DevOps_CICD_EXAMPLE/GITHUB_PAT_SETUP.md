# Create a New GitHub Personal Access Token (PAT)

Use this guide when you need to create a token for GitHub Actions secrets such as DEV_PAGES_TOKEN.

## Steps

1. **Open GitHub Settings**
   Click your profile icon → **Settings**.

2. **Go to Developer Settings**
   Scroll down the left sidebar → **Developer settings**.

3. **Open Personal Access Tokens**
   Choose either:
   - **Fine-grained tokens** (recommended)
   - **Classic tokens** (legacy)

4. **Create a New Token**
   Select **Generate new token**.

5. **Set Token Name & Expiration**
   Give it a descriptive name and choose an expiration date.

6. **Select Repository Access**
   Pick the specific repositories the token should access.

7. **Choose Permissions**
   Enable only what you need. For this repository’s dev publish flow, the token should have access to write to the target dev-lane repository. In a fine-grained PAT, grant **Contents: Read and write**.

8. **Generate Token**
   Click **Generate** and copy the token immediately.

9. **Store Securely**
   Paste the token into your GitHub Actions secret or other secure location. GitHub will never show it again.
