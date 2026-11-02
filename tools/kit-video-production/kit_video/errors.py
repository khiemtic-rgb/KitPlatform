"""Structured production errors. Status is FAIL or BLOCKED."""


class ProductionError(Exception):
    def __init__(self, status: str, errors: list[dict]):
        self.status = status
        self.errors = errors
        super().__init__(status + ": " + "; ".join(item["message"] for item in errors))


def issue(code: str, path: str, message: str) -> dict:
    return {"code": code, "path": path, "message": message}
