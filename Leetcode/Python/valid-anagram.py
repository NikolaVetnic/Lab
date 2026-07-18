import argparse
import time
from collections.abc import Callable


LOWERCASE_A_AS_INT = 97


class Solution:
    def valid_anagram(self, s: str, t: str) -> bool:
        if len(s) != len(t):
            return False

        for char in s:
            t = t.replace(char, "", 1)

        return len(t) == 0

    def valid_anagram_sort(self, s: str, t: str) -> bool:
        if len(s) != len(t):
            return False

        return sorted(s.lower()) == sorted(t.lower())

    def valid_anagram_freq(self, s: str, t: str) -> bool:
        if len(s) != len(t):
            return False

        s = s.lower()
        t = t.lower()

        freq = {}

        for char in s:
            idx = ord(char) - LOWERCASE_A_AS_INT

            if idx not in freq:
                freq[idx] = 1
            else:
                freq[idx] += 1

        for char in t:
            idx = ord(char) - LOWERCASE_A_AS_INT

            if idx in freq:
                freq[idx] -= 1

        return all(v == 0 for v in freq.values())


def parse_arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Check if the target string is a valid anagram of the source."
    )

    parser.add_argument(
        "-s",
        "--source",
        type=str,
        required=True,
        help="Source string."
    )


    parser.add_argument(
        "-t",
        "--target",
        type=str,
        required=True,
        help="Target string."
    )

    return parser.parse_args()


def measure_execution(
    name: str,
    function: Callable[[str, str], bool],
    source: str,
    target: str,
    iterations: int = 100_000,
) -> None:
    start = time.perf_counter()

    result = False

    for _ in range(iterations):
        result = function(source, target)

    elapsed = time.perf_counter() - start
    average = elapsed / iterations

    print(f"{name}:")
    print(f"  Result: {result}")
    print(f"  Total: {elapsed:.6f} s")
    print(f"  Average: {average * 1_000_000:.3f} µs")
    print()


def main() -> None:
    args = parse_arguments()
    solution = Solution()

    measure_execution(
        "Brute force",
        solution.valid_anagram,
        args.source,
        args.target,
    )

    measure_execution(
        "Frequency counting",
        solution.valid_anagram_freq,
        args.source,
        args.target,
    )

    measure_execution(
        "Sorting",
        solution.valid_anagram_sort,
        args.source,
        args.target,
    )


if __name__ == "__main__":
    main()
