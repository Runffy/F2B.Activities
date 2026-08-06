#Requires -Version 5.0
<#
.SYNOPSIS
  GUI multi-line commit message, then git add / commit / push (Gitee).
#>
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location -LiteralPath $ScriptDir

function Show-CommitMessageDialog {
    $form = New-Object System.Windows.Forms.Form
    $form.Text = 'Commit to Gitee'
    $form.StartPosition = 'CenterScreen'
    $form.Size = New-Object System.Drawing.Size(560, 420)
    $form.MinimumSize = New-Object System.Drawing.Size(420, 300)
    $form.Font = New-Object System.Drawing.Font('Segoe UI', 10)
    $form.MaximizeBox = $true
    $form.MinimizeBox = $false
    $form.ShowInTaskbar = $true
    $form.TopMost = $true

    $label = New-Object System.Windows.Forms.Label
    $label.Text = 'Commit message (multi-line OK):'
    $label.AutoSize = $true
    $label.Location = New-Object System.Drawing.Point(12, 12)

    $box = New-Object System.Windows.Forms.TextBox
    $box.Multiline = $true
    $box.ScrollBars = 'Both'
    $box.AcceptsReturn = $true
    $box.AcceptsTab = $true
    $box.WordWrap = $true
    $box.Font = New-Object System.Drawing.Font('Consolas', 10)
    $box.Anchor = 'Top,Bottom,Left,Right'
    $box.Location = New-Object System.Drawing.Point(12, 40)
    $box.Size = New-Object System.Drawing.Size(520, 280)

    $btnOk = New-Object System.Windows.Forms.Button
    $btnOk.Text = 'Commit and Push'
    $btnOk.Anchor = 'Bottom,Right'
    $btnOk.Size = New-Object System.Drawing.Size(120, 32)
    $btnOk.Location = New-Object System.Drawing.Point(300, 335)
    $btnOk.DialogResult = [System.Windows.Forms.DialogResult]::OK

    $btnCancel = New-Object System.Windows.Forms.Button
    $btnCancel.Text = 'Cancel'
    $btnCancel.Anchor = 'Bottom,Right'
    $btnCancel.Size = New-Object System.Drawing.Size(100, 32)
    $btnCancel.Location = New-Object System.Drawing.Point(432, 335)
    $btnCancel.DialogResult = [System.Windows.Forms.DialogResult]::Cancel

    $form.Controls.AddRange(@($label, $box, $btnOk, $btnCancel))
    $form.AcceptButton = $btnOk
    $form.CancelButton = $btnCancel
    $form.Add_Shown({ $box.Focus() })

    $result = $form.ShowDialog()
    if ($result -ne [System.Windows.Forms.DialogResult]::OK) {
        return $null
    }

    $text = $box.Text
    if ([string]::IsNullOrWhiteSpace($text)) {
        [System.Windows.Forms.MessageBox]::Show(
            'Commit message cannot be empty.',
            'Commit to Gitee',
            [System.Windows.Forms.MessageBoxButtons]::OK,
            [System.Windows.Forms.MessageBoxIcon]::Warning) | Out-Null
        return $null
    }

    return $text.TrimEnd()
}

function Invoke-Git {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    Write-Host ('> git ' + ($Arguments -join ' ')) -ForegroundColor Cyan
    & git @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "git $($Arguments[0]) failed with exit code $LASTEXITCODE"
    }
}

try {
    if (-not (Test-Path -LiteralPath (Join-Path $ScriptDir '.git'))) {
        throw "Not a git repository: $ScriptDir"
    }

    $message = Show-CommitMessageDialog
    if ($null -eq $message) {
        Write-Host 'Cancelled.' -ForegroundColor Yellow
        exit 0
    }

    Write-Host "Repository: $ScriptDir" -ForegroundColor Gray
    Write-Host 'Commit message:' -ForegroundColor Gray
    Write-Host $message
    Write-Host ''

    Invoke-Git -Arguments @('add', '.')

    $msgFile = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), ('f2b-commit-' + [Guid]::NewGuid().ToString('N') + '.txt'))
    try {
        # UTF-8 without BOM — safer for git on Windows
        $utf8NoBom = New-Object System.Text.UTF8Encoding $false
        [System.IO.File]::WriteAllText($msgFile, $message, $utf8NoBom)
        Invoke-Git -Arguments @('commit', '-F', $msgFile)
    }
    finally {
        if (Test-Path -LiteralPath $msgFile) {
            Remove-Item -LiteralPath $msgFile -Force -ErrorAction SilentlyContinue
        }
    }

    # Explicit remote: this repo has both origin (GitHub) and gitee.
    # Equivalent to first-time: git push -u gitee main
    $branch = (& git rev-parse --abbrev-ref HEAD).Trim()
    if ([string]::IsNullOrWhiteSpace($branch) -or $branch -eq 'HEAD') {
        $branch = 'main'
    }
    Invoke-Git -Arguments @('push', '-u', 'gitee', $branch)

    Write-Host ''
    Write-Host 'Done: committed and pushed to gitee.' -ForegroundColor Green
    [System.Windows.Forms.MessageBox]::Show(
        "Committed and pushed to gitee ($branch).",
        'Commit to Gitee',
        [System.Windows.Forms.MessageBoxButtons]::OK,
        [System.Windows.Forms.MessageBoxIcon]::Information) | Out-Null
}
catch {
    Write-Host ''
    Write-Host $_.Exception.Message -ForegroundColor Red
    [System.Windows.Forms.MessageBox]::Show(
        $_.Exception.Message,
        'Commit to Gitee - Error',
        [System.Windows.Forms.MessageBoxButtons]::OK,
        [System.Windows.Forms.MessageBoxIcon]::Error) | Out-Null
    exit 1
}
