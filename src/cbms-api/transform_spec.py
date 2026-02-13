"""Transforms the original CBMS API spec into a version with descriptive schema names.

The original spec uses generic names (type, type_1, etc.) and inline nested
objects. This script:
  1. Renames schema definitions and $ref references to descriptive names
  2. Extracts inline nested objects into named top-level schemas

Usage: python3 transform_spec.py <input_file> <output_file>
  input_file:  api-spec.original.yaml
  output_file: api-spec.yaml
"""

import re
import sys


# --- Step 1: Rename $ref values and schema definition keys ---

# Mapping from original schema names to descriptive names.
# Order matters: longer names first to avoid partial matches (e.g., "type_1" before "type").
SCHEMA_RENAMES = [
    ("type_1", "CheckEnrollmentResponse"),
    ("type_2", "ErrorResponse"),
    ("type_3", "GetAccountDetailsRequest"),
    ("type_4", "GetAccountDetailsResponse"),
    ("type_5", "UpdateStudentDetailsRequest"),
    ("type_6", "UpdateStudentDetailsResponse"),
    ("type",   "CheckEnrollmentRequest"),
]

# --- Step 2: Extract inline nested objects into top-level schemas ---

# Each entry: (parent_property_name, indentation_of_items, new_schema_name, is_array_items)
# is_array_items=True  means the inline object is under "items:" of an array property
# is_array_items=False means the inline object IS the property value directly
EXTRACTIONS = [
    ("stdntDtls",      10, "CheckEnrollmentStudentDetail", True),
    ("errorDetails",   10, "ErrorDetail",                  True),
    ("stdntEnrollDtls", 10, "GetAccountStudentDetail",      True),
    ("addr",           8,  "Address",                      False),
]


def rename_schemas(lines):
    """Rename $ref values and schema definition keys to descriptive names."""
    renamed_lines = []
    for line in lines:
        for old_name, new_name in SCHEMA_RENAMES:
            # Rename $ref references: schemas/type_1" -> schemas/CheckEnrollmentResponse"
            line = line.replace(f"schemas/{old_name}\"", f"schemas/{new_name}\"")
            # Rename schema definition keys (4-space indented, start of YAML key)
            line = re.sub(
                rf"^(    ){re.escape(old_name)}:(\s*)$",
                rf"\1{new_name}:\2",
                line,
            )
        renamed_lines.append(line)
    return renamed_lines


def find_block_end(all_lines, start_idx, base_indent):
    """Find the end of a YAML block starting at start_idx with the given base indentation.
    Returns the index of the first line that is at or below the base indentation level
    (i.e., a sibling or parent), or len(all_lines) if the block extends to end of file."""
    for i in range(start_idx + 1, len(all_lines)):
        stripped = all_lines[i].rstrip("\n")
        if stripped == "":
            continue
        line_indent = len(stripped) - len(stripped.lstrip())
        if line_indent <= base_indent:
            return i
    return len(all_lines)


def extract_inline_to_ref(all_lines, prop_name, items_indent, new_schema_name, is_array_items):
    """Replace an inline object definition with a $ref and return the extracted schema lines."""
    extracted_properties = []
    result_lines = []
    i = 0

    while i < len(all_lines):
        stripped = all_lines[i].rstrip("\n").rstrip()

        if is_array_items:
            # Look for: "          items:" followed by "            type: object"
            # under a property named prop_name
            prop_pattern = f"{' ' * (items_indent - 2)}{prop_name}:"
            if stripped == prop_pattern or stripped.startswith(prop_pattern + " "):
                # Found the array property line. Next should be "type: array", then "items:"
                result_lines.append(all_lines[i])
                i += 1
                # Copy "type: array" line
                if i < len(all_lines) and "type: array" in all_lines[i]:
                    result_lines.append(all_lines[i])
                    i += 1
                # Now expect "items:" line
                if i < len(all_lines) and "items:" in all_lines[i]:
                    items_line_indent = len(all_lines[i].rstrip("\n")) - len(all_lines[i].rstrip("\n").lstrip())
                    # Replace "items:\n            type: object\n            properties:\n..."
                    # with "items:\n              $ref: ..."
                    ref_indent = " " * (items_line_indent + 2)
                    result_lines.append(f"{' ' * items_line_indent}items:\n")
                    result_lines.append(f'{ref_indent}$ref: "#/components/schemas/{new_schema_name}"\n')
                    i += 1
                    # Skip "type: object" line
                    if i < len(all_lines) and "type: object" in all_lines[i]:
                        i += 1
                    # Now find and extract the properties block
                    if i < len(all_lines) and "properties:" in all_lines[i]:
                        props_indent = len(all_lines[i].rstrip("\n")) - len(all_lines[i].rstrip("\n").lstrip())
                        i += 1  # skip "properties:" line
                        block_end = find_block_end(all_lines, i - 1, props_indent)
                        # Collect property lines, de-indented to top-level schema format
                        for j in range(i, block_end):
                            raw = all_lines[j].rstrip("\n")
                            if raw.strip() == "":
                                continue
                            current_indent = len(raw) - len(raw.lstrip())
                            new_indent = current_indent - props_indent + 6
                            extracted_properties.append(" " * max(new_indent, 0) + raw.lstrip() + "\n")
                        i = block_end
                        continue
                continue
        else:
            # Direct object property (not array items)
            prop_pattern = f"{' ' * (items_indent)}{prop_name}:"
            if stripped == prop_pattern or stripped.startswith(prop_pattern + " "):
                prop_line_indent = len(all_lines[i].rstrip("\n")) - len(all_lines[i].rstrip("\n").lstrip())
                # Check next line for "type: object"
                if i + 1 < len(all_lines) and "type: object" in all_lines[i + 1]:
                    ref_indent = " " * (prop_line_indent + 2)
                    result_lines.append(f"{' ' * prop_line_indent}{prop_name}:\n")
                    result_lines.append(f'{ref_indent}$ref: "#/components/schemas/{new_schema_name}"\n')
                    i += 1  # skip the prop_name line (already written)
                    i += 1  # skip "type: object"
                    # Skip "properties:" and extract its children
                    if i < len(all_lines) and "properties:" in all_lines[i]:
                        props_indent = len(all_lines[i].rstrip("\n")) - len(all_lines[i].rstrip("\n").lstrip())
                        i += 1  # skip "properties:"
                        block_end = find_block_end(all_lines, i - 1, props_indent)
                        for j in range(i, block_end):
                            raw = all_lines[j].rstrip("\n")
                            if raw.strip() == "":
                                continue
                            current_indent = len(raw) - len(raw.lstrip())
                            new_indent = current_indent - props_indent + 6
                            extracted_properties.append(" " * max(new_indent, 0) + raw.lstrip() + "\n")
                        i = block_end
                        continue
                    continue

        result_lines.append(all_lines[i])
        i += 1

    return result_lines, extracted_properties


def insert_extracted_schemas(lines, extracted_schemas):
    """Insert extracted schemas before UpdateStudentDetailsResponse.
    This keeps them grouped near the schemas that reference them."""
    output_lines = []
    for line in lines:
        if line.strip().startswith("UpdateStudentDetailsResponse:"):
            for schema_name, props in extracted_schemas.items():
                output_lines.append(f"    {schema_name}:\n")
                output_lines.append(f"      type: object\n")
                output_lines.append(f"      properties:\n")
                for prop_line in props:
                    output_lines.append(prop_line)
        output_lines.append(line)
    return output_lines


def main():
    if len(sys.argv) != 3:
        print(f"Usage: {sys.argv[0]} <input_file> <output_file>", file=sys.stderr)
        sys.exit(1)

    input_file = sys.argv[1]
    output_file = sys.argv[2]

    with open(input_file, "r") as f:
        lines = f.readlines()

    lines = rename_schemas(lines)

    # Apply extractions one at a time, collecting the extracted schemas
    extracted_schemas = {}
    for prop_name, indent, schema_name, is_array in EXTRACTIONS:
        lines, props = extract_inline_to_ref(lines, prop_name, indent, schema_name, is_array)
        if props:
            extracted_schemas[schema_name] = props

    lines = insert_extracted_schemas(lines, extracted_schemas)

    with open(output_file, "w") as f:
        f.writelines(lines)

    print(f"Transformed {input_file} -> {output_file}")


if __name__ == "__main__":
    main()
