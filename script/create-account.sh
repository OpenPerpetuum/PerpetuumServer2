#!/usr/bin/env bash
# Creates a new account in Perpetuum Server database with optional admin access level.
set -eo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

COMPOSE_FILE="${REPO_ROOT}/compose.yml"
ENV_FILE="${REPO_ROOT}/.env.local"

# Colors for terminal output
RED='\033[0;31m'
GREEN='\033[0;32m'
CYAN='\033[0;36m'
YELLOW='\033[1;33m'
BOLD='\033[1m'
NC='\033[0m' # No Color

print_help() {
    echo -e "${BOLD}Usage:${NC} $0 [options]"
    echo ""
    echo -e "${BOLD}Options:${NC}"
    echo "  -e, --email <email|username>   Account email or username (if omitted, will be prompted)"
    echo "  -p, --password <password>      Account password (if omitted, will be prompted securely)"
    echo "  -a, --admin                    Grant admin privileges (AccessLevel 14 / toolAdmin)"
    echo "      --no-admin                 Create standard player account (AccessLevel 2 / normal)"
    echo "  -h, --help                     Show this help message"
    echo ""
    echo -e "${BOLD}Environment variables:${NC}"
    echo "  EMAIL=<email|username>"
    echo "  PASSWORD=<password>"
    echo "  ADMIN=true|false|1|0"
    echo ""
    echo -e "${BOLD}Examples:${NC}"
    echo "  $0                                      # Interactive wizard (prompts for email, hidden password, admin)"
    echo "  $0 -e player1                           # Prompts securely for password (hidden input)"
    echo "  $0 -e player1 -p secret123              # Standard player account"
    echo "  $0 -e gm_admin -p secret123 --admin     # Admin account"
    echo "  make create-account EMAIL=gm ADMIN=true # Prompts securely for password"
}

EMAIL_VAL="${EMAIL:-}"
PASSWORD_VAL="${PASSWORD:-${PASS:-}}"
ADMIN_VAL=""

if [ -n "${ADMIN:-}" ]; then
    case "${ADMIN,,}" in
        true|1|yes|y) ADMIN_VAL="1" ;;
        false|0|no|n) ADMIN_VAL="0" ;;
    esac
fi

EXPLICIT_EMAIL=0
if [ -n "${EMAIL_VAL}" ]; then
    EXPLICIT_EMAIL=1
fi

EXPLICIT_ADMIN=0
if [ -n "${ADMIN_VAL}" ]; then
    EXPLICIT_ADMIN=1
fi

# Parse CLI arguments
while [[ $# -gt 0 ]]; do
    case "$1" in
        -e|--email|--user|--username)
            EMAIL_VAL="$2"
            EXPLICIT_EMAIL=1
            shift 2
            ;;
        -p|--password|--pass)
            PASSWORD_VAL="$2"
            shift 2
            ;;
        -a|--admin)
            ADMIN_VAL="1"
            EXPLICIT_ADMIN=1
            shift
            ;;
        --no-admin)
            ADMIN_VAL="0"
            EXPLICIT_ADMIN=1
            shift
            ;;
        -h|--help)
            print_help
            exit 0
            ;;
        *)
            echo -e "${RED}Error: Unknown option '$1'${NC}" >&2
            echo "Use -h or --help for usage information." >&2
            exit 1
            ;;
    esac
done

# Helper function to prompt interactively even when invoked via make/subshell
read_prompt() {
    local prompt_msg="$1"
    local is_secret="${2:-0}"
    local input=""

    if [ -t 0 ]; then
        if [ "${is_secret}" -eq 1 ]; then
            read -r -s -p "${prompt_msg}" input
            echo "" >&2
        else
            read -r -p "${prompt_msg}" input
        fi
    elif [ -r /dev/tty ]; then
        if [ "${is_secret}" -eq 1 ]; then
            read -r -s -p "${prompt_msg}" input </dev/tty
            echo "" >&2
        else
            read -r -p "${prompt_msg}" input </dev/tty
        fi
    fi
    echo "${input}"
}

IS_INTERACTIVE=0
if [ -t 0 ] || [ -r /dev/tty ]; then
    IS_INTERACTIVE=1
fi

# Prompt for missing email
if [ -z "${EMAIL_VAL}" ]; then
    if [ "${IS_INTERACTIVE}" -eq 1 ]; then
        while true; do
            EMAIL_VAL=$(read_prompt "Enter account email / username: " 0)
            EMAIL_VAL="$(echo -n "${EMAIL_VAL}" | xargs)"
            if [ -n "${EMAIL_VAL}" ]; then
                break
            fi
            echo -e "${RED}Account email/username cannot be empty. Please try again.${NC}" >&2
        done
    else
        echo -e "${RED}Error: Account email/username is required in non-interactive mode (-e or EMAIL=...)${NC}" >&2
        exit 1
    fi
fi

# Trim whitespace from email
EMAIL_VAL="$(echo -n "${EMAIL_VAL}" | xargs)"

if [ -z "${EMAIL_VAL}" ]; then
    echo -e "${RED}Error: Account email/username cannot be empty.${NC}" >&2
    exit 1
fi

# Prompt securely for missing password (hiding characters)
if [ -z "${PASSWORD_VAL}" ]; then
    if [ "${IS_INTERACTIVE}" -eq 1 ]; then
        while true; do
            PW_1=$(read_prompt "Enter password (hidden): " 1)
            if [ -z "${PW_1}" ]; then
                echo -e "${RED}Password cannot be empty. Please try again.${NC}" >&2
                continue
            fi

            PW_2=$(read_prompt "Confirm password (hidden): " 1)
            if [ "${PW_1}" != "${PW_2}" ]; then
                echo -e "${RED}Passwords do not match. Please try again.${NC}" >&2
                continue
            fi

            PASSWORD_VAL="${PW_1}"
            break
        done
    else
        echo -e "${RED}Error: Password is required in non-interactive mode (-p or PASSWORD=...)${NC}" >&2
        exit 1
    fi
fi

if [ -z "${PASSWORD_VAL}" ]; then
    echo -e "${RED}Error: Password cannot be empty.${NC}" >&2
    exit 1
fi

# Compute SHA-1 hash of password (ASCII encoding) to match Perpetuum authentication
if [[ ${#PASSWORD_VAL} -eq 40 && "${PASSWORD_VAL}" =~ ^[0-9a-fA-F]{40}$ ]]; then
    PASSWORD_HASH="${PASSWORD_VAL^^}"
else
    PASSWORD_HASH=$(echo -n "${PASSWORD_VAL}" | sha1sum | awk '{print toupper($1)}')
fi

# Determine admin status
if [ -z "${ADMIN_VAL}" ]; then
    if [ "${IS_INTERACTIVE}" -eq 1 ] && [ "${EXPLICIT_EMAIL}" -eq 0 ] && [ "${EXPLICIT_ADMIN}" -eq 0 ]; then
        ADMIN_INPUT=$(read_prompt "Make this account an admin? [y/N]: " 0)
        case "${ADMIN_INPUT,,}" in
            y|yes|1|true) ADMIN_VAL="1" ;;
            *) ADMIN_VAL="0" ;;
        esac
    else
        ADMIN_VAL="0"
    fi
fi

if [ ! -f "${ENV_FILE}" ]; then
    echo -e "${RED}Error: Environment file ${ENV_FILE} not found.${NC}" >&2
    exit 1
fi

compose() {
    docker compose -f "${COMPOSE_FILE}" --env-file "${ENV_FILE}" "$@"
}

# Ensure database container is running and healthy
db_status=$(compose ps -a --format '{{.State}}' db 2>/dev/null || echo "not_found")
if [ "${db_status}" != "running" ]; then
    echo -e "${CYAN}Database container is not running. Starting database service...${NC}"
    compose up -d db --wait
fi

# Escape single quotes for SQL email
EMAIL_SQL="${EMAIL_VAL//\'/\'\'}"

SQL_SCRIPT=$(cat <<EOF
SET NOCOUNT ON;

DECLARE @email VARCHAR(50) = '${EMAIL_SQL}';
DECLARE @passwordHash VARCHAR(40) = '${PASSWORD_HASH}';
DECLARE @isAdmin INT = ${ADMIN_VAL};

IF EXISTS (SELECT 1 FROM [dbo].[accounts] WHERE [email] = @email)
BEGIN
    SELECT -1 AS [accountID], 0 AS [accLevel], 'EXISTS' AS [status];
    RETURN;
END

DECLARE @accLevel INT = CASE WHEN @isAdmin = 1 THEN 14 ELSE 2 END;
DECLARE @campaignId VARCHAR(512) = CASE WHEN @isAdmin = 1 THEN '{"host":"tooladmin"}' ELSE '{"host":"opencreate"}' END;

INSERT INTO [dbo].[accounts]
    ([email],
     [password],
     [accLevel],
     [state],
     [emailConfirmed],
     [isactive],
     [creation],
     [totalMinsOnline],
     [clientType],
     [isLoggedIn],
     [banlength],
     [credit],
     [resetcount],
     [wasreset],
     [payingcustomer],
     [campaignid])
VALUES
    (@email,
     @passwordHash,
     @accLevel,
     1,
     1,
     1,
     GETDATE(),
     0,
     0,
     0,
     0,
     0,
     0,
     0,
     0,
     @campaignId);

DECLARE @newAccountId INT = SCOPE_IDENTITY();
SELECT @newAccountId AS [accountID], @accLevel AS [accLevel], 'SUCCESS' AS [status];
EOF
)

# Run sqlcmd inside db container
OUTPUT=$(compose exec -T db /bin/bash -c "
/opt/mssql-tools18/bin/sqlcmd -S localhost -C -U sa -P \"\$MSSQL_SA_PASSWORD\" -d perpetuumsa -W -h -1 -s '|' -Q \"${SQL_SCRIPT}\"
" 2>&1)

# Check for database execution errors
if [ $? -ne 0 ]; then
    echo -e "${RED}Error executing database query:${NC}" >&2
    echo "${OUTPUT}" >&2
    exit 1
fi

# Parse pipe-delimited result
RESULT_LINE=$(echo "${OUTPUT}" | grep -E '^[0-9-]+ *\|' | tail -n 1 || true)

if [ -z "${RESULT_LINE}" ]; then
    echo -e "${RED}Unexpected response from database:${NC}" >&2
    echo "${OUTPUT}" >&2
    exit 1
fi

ACCOUNT_ID=$(echo "${RESULT_LINE}" | awk -F'|' '{print $1}' | tr -d '[:space:]')
ACC_LEVEL=$(echo "${RESULT_LINE}" | awk -F'|' '{print $2}' | tr -d '[:space:]')
STATUS=$(echo "${RESULT_LINE}" | awk -F'|' '{print $3}' | tr -d '[:space:]')

if [ "${STATUS}" = "EXISTS" ] || [ "${ACCOUNT_ID}" = "-1" ]; then
    echo -e "${RED}Error: Account with email/username '${EMAIL_VAL}' already exists.${NC}" >&2
    exit 1
fi

if [ "${STATUS}" = "SUCCESS" ]; then
    echo ""
    echo -e "${GREEN}${BOLD}✓ Account created successfully!${NC}"
    echo "----------------------------------------"
    printf "  %-18s: %s\n" "Account ID" "${ACCOUNT_ID}"
    printf "  %-18s: %s\n" "Username / Email" "${EMAIL_VAL}"
    if [ "${ACC_LEVEL}" = "14" ]; then
        printf "  %-18s: %s\n" "Role" "Admin (Level 14 / toolAdmin)"
    else
        printf "  %-18s: %s\n" "Role" "Player (Level 2 / normal)"
    fi
    printf "  %-18s: %s\n" "Status" "Active (Email Confirmed)"
    echo "----------------------------------------"
    echo ""
    exit 0
fi

echo -e "${RED}Failed to create account. Result: ${OUTPUT}${NC}" >&2
exit 1
