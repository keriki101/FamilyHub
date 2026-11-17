#!/usr/bin/env python3

import base64
import getpass
import hashlib
import secrets
import sys


iterations = 600_000
password = getpass.getpass("FamilyHub password (minimum 16 characters): ")
confirmation = getpass.getpass("Confirm password: ")

if len(password) < 16:
    sys.exit("Password must contain at least 16 characters.")
if not secrets.compare_digest(password, confirmation):
    sys.exit("Passwords do not match.")

salt = secrets.token_bytes(16)
password_hash = hashlib.pbkdf2_hmac("sha256", password.encode(), salt, iterations, dklen=32)
print(
    "pbkdf2-sha256"
    f"${iterations}"
    f"${base64.b64encode(salt).decode()}"
    f"${base64.b64encode(password_hash).decode()}"
)
