"""Minimal ASCII DXF reader that retains repeated codes and sort handles."""
def records(path):
    lines = path.read_text(encoding='utf-8', errors='replace').splitlines()
    result, current = [], []
    for i in range(0, len(lines) - 1, 2):
        code, value = int(lines[i]), lines[i + 1]
        if code == 0 and current:
            result.append(current)
            current = []
        current.append((code, value))
    if current:
        result.append(current)
    return result

def value(record, code, default=None):
    return next((v for c, v in record if c == code), default)

def block_orders(path, predicate):
    data = records(path)
    sorts = {}
    for record in data:
        if record[0] != (0, 'SORTENTSTABLE'):
            continue
        subclass = record.index((100, 'AcDbSortentsTable'))
        owner = value(record[subclass:], 330)
        mapping, entity = {}, None
        for code, val in record[subclass:]:
            if code == 331:
                entity = val
            elif code == 5 and entity:
                mapping[entity] = val
                entity = None
        sorts[owner] = mapping
    result, block = {}, None
    for record in data:
        if record[0] == (0, 'BLOCK'):
            name = value(record, 2)
            block = (name, value(record, 330), []) if predicate(name) else None
        elif record[0] == (0, 'ENDBLK'):
            if block:
                name, owner, entities = block
                mapping = sorts.get(owner, {})
                result[name] = sorted(entities, key=lambda r: int(mapping.get(value(r, 5), value(r, 5)), 16))
            block = None
        elif block:
            block[2].append(record)
    return result
