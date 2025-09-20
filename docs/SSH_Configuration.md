# SSH Configuration Guide

## Overview

The CoverageAnalyzer now supports advanced SSH configuration through SSH config files and secure credential management. This eliminates the need to store SSH passwords in project files and provides flexible authentication methods.

## Features

### 1. SSH Config File Support
- Reads SSH configuration from `~/.ssh/config`
- Supports host-specific settings including:
  - Custom usernames
  - Multiple identity files (SSH keys)
  - Custom ports
  - Authentication preferences

### 2. Multiple Authentication Methods
- **Primary**: SSH key authentication using configured identity files
- **Fallback**: Username/password dialog when keys fail
- **Auto-discovery**: Automatically finds available SSH keys

### 3. Secure Credential Handling
- No passwords stored in project files
- Interactive credential prompts when needed
- SSH keys preferred over passwords

## SSH Configuration

### Setting up SSH Config

Create or edit `~/.ssh/config` with host-specific settings:

```ssh-config
# Global defaults
User myusername
Port 22
PubkeyAuthentication yes
PasswordAuthentication yes

# Host-specific configuration
Host atlvscode0003
    HostName atlvscode0003.amd.com
    User myusername
    IdentityFile ~/.ssh/id_rsa
    IdentityFile ~/.ssh/id_ed25519
    Port 22
    PubkeyAuthentication yes
    PasswordAuthentication no

# Wildcard patterns supported
Host *.amd.com
    User amduser
    IdentityFile ~/.ssh/amd_key
```

### SSH Key Setup

1. **Generate SSH keys** (if not already done):
   ```bash
   ssh-keygen -t rsa -b 4096 -f ~/.ssh/id_rsa
   # or
   ssh-keygen -t ed25519 -f ~/.ssh/id_ed25519
   ```

2. **Copy public key to server**:
   ```bash
   ssh-copy-id username@atlvscode0003
   ```

3. **Test connection**:
   ```bash
   ssh atlvscode0003
   ```

### Supported Identity Files

The application will automatically try these identity files in order:
- Files specified in SSH config `IdentityFile` directives
- Default files: `~/.ssh/id_rsa`, `~/.ssh/id_ecdsa`, `~/.ssh/id_ed25519`

## Authentication Flow

1. **SSH Config Parsing**: Application reads `~/.ssh/config` for host settings
2. **Key Authentication**: Tries each configured identity file
3. **Fallback Dialog**: Shows credential prompt if keys fail
4. **Connection**: Establishes SSH and SFTP connections for file transfer

## User Interface Changes

### Simplified SSH Configuration Panel
- **SSH Host**: Only field required (defaults to `atlvscode0003`)
- **Authentication Method**: Automatically determined
- **No stored credentials**: Username/password handled securely

### Authentication Dialog
When SSH keys fail, a secure dialog prompts for:
- Username (pre-filled from SSH config if available)
- Password (not stored)
- Connection retry options

## Migration from Previous Version

### For Existing Projects
- Old projects with stored SSH credentials will continue to work
- New authentication method will be used for new connections
- Recommend deleting stored passwords from project files

### For New Projects
- Only SSH host needs to be configured
- Authentication handled automatically via SSH config
- More secure and flexible than previous approach

## Troubleshooting

### SSH Key Issues
1. **Verify key permissions**:
   ```bash
   chmod 600 ~/.ssh/id_rsa
   chmod 644 ~/.ssh/id_rsa.pub
   ```

2. **Check SSH config syntax**:
   ```bash
   ssh -F ~/.ssh/config -T atlvscode0003
   ```

3. **Debug SSH connection**:
   ```bash
   ssh -v atlvscode0003
   ```

### Common Problems

| Problem | Solution |
|---------|----------|
| "SSH key not found" | Ensure key files exist and have correct permissions |
| "Authentication failed" | Verify SSH config syntax and test manual SSH connection |
| "No SSH config found" | Create `~/.ssh/config` or use credential dialog |
| "Connection timeout" | Check host connectivity and firewall settings |

### Application Logs

The application provides detailed SSH authentication logs:
- SSH config parsing results
- Identity file discovery
- Authentication method attempts
- Connection status

## Security Benefits

1. **No stored passwords**: Credentials never saved to disk
2. **SSH key preference**: More secure than password authentication
3. **Interactive prompts**: User controls credential disclosure
4. **Config file flexibility**: Standard SSH configuration approach

## Advanced Configuration

### Multiple Keys per Host
```ssh-config
Host atlvscode0003
    IdentityFile ~/.ssh/work_key
    IdentityFile ~/.ssh/personal_key
    IdentityFile ~/.ssh/backup_key
```

### Host Aliases
```ssh-config
Host vscode
    HostName atlvscode0003.amd.com
    User myusername
    IdentityFile ~/.ssh/id_rsa
```

### Port Forwarding
```ssh-config
Host atlvscode0003
    LocalForward 8080 localhost:8080
    RemoteForward 9090 localhost:9090
```

## Best Practices

1. **Use SSH keys**: Set up key-based authentication
2. **Organize config**: Use clear host naming and grouping
3. **Regular key rotation**: Update SSH keys periodically
4. **Test connections**: Verify SSH access before using application
5. **Backup keys**: Securely store SSH key backups