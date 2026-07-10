"""
@FileName: line_counter.py
@Description: Count lines of single file or files in directory.
@Author: evemia-yuri
@CreatedAt: 2026-4-20
@Version: v1.0
@Usage:
    - Count lines single file:
        python3 line_counter.py <target_file>
    - Count lines of files in directory:
        python3 line_counter.py -d <target_directory> -e <target_file_extension> -x <exclude_directory>
@Sample:
    - Count lines single file:
        python3 line_counter.py README.md
    - Count lines of files in directory:
        python3 line_counter.py -d server -e .cs -x bin obj
        python3 line_counter.py -d app -e .ts (exclude directory can be empty)
"""


import pathlib
import argparse
from typing import List


def count_lines_in_file(file_name: pathlib.Path) -> int:
    with open(file_name, "rb") as file:
        count = 0
        buf_size = 1024 * 1024
        buf = file.read(buf_size)
        while buf:
            count += buf.count(b"\n")
            buf = file.read(buf_size)
        return count


def count_lines_in_dir(dir_name: pathlib.Path, file_ext: str, excluded_dir: List[str]) -> int:
    total_lines = 0
    if not file_ext.startswith("."):
        file_ext = f".{file_ext}"

    for entry in dir_name.iterdir():
        if entry.is_file() and entry.suffix == file_ext:
            total_lines += count_lines_in_file(entry)
        elif entry.is_dir() and entry.name not in excluded_dir:
            total_lines += count_lines_in_dir(entry, file_ext, excluded_dir)
    return total_lines


def main() -> None:
    parser = argparse.ArgumentParser()

    parser.add_argument("file", nargs="?", type=str, help="Target File")
    parser.add_argument("-d", "--directory", type=str, help="Target Directory")
    parser.add_argument("-e", "--extension", type=str, help="File Extension")
    parser.add_argument("-x", "--exclude", nargs="+", type=str, default=[], help="Excluded Directory")

    args = parser.parse_args()
    if args.file:
        if args.directory or args.extension:
            print("[Warning] Since a single file has been specified.")

        file = pathlib.Path(args.file)
        if file.is_file():
            print(f"[Info] Start counting lines of file ''{file.name}...")
            lines = count_lines_in_file(file.absolute())
            print(f"[Info] Total lines is {lines}.")
        else:
            print(f"[Error] '{file.name}' is not a valid file.")
    elif args.directory:
        if not args.extension:
            parser.error("Must use '-e/--extension' to specify file extension.")

        directory = pathlib.Path(args.directory)
        if directory.is_dir():
            exclude_info = f" (excluding: {', '.join(args.exclude)})" if args.exclude else ""
            print(f"[Info] Start counting lines of '{args.extension}' files in directory '{directory.name}'{exclude_info}...")
            lines = count_lines_in_dir(directory.absolute(), args.extension, args.exclude)
            print(f"[Info] Total lines is {lines}.")
        else:
            print(f"[Error] '{directory.name}' is not a valid directory.")
    else:
        parser.print_help()


if __name__ == "__main__":
    main()
