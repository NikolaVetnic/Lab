import argparse


class Solution:
    def contains_duplicate(self, nums: list[int]) -> bool:
        for i in range(len(nums) - 1):
            for j in range(i + 1, len(nums)):
                if (nums[i] == nums[j]):
                    return True

        return False

    def contains_duplicate_opt(self, nums: list[int]) -> bool:
        seen = set()

        for num in nums:
            if num in seen:
                return True
            seen.add(num)

        return False

    def contains_duplicate_py(self, nums: list[int]) -> bool:
        return len(set(nums)) < len(nums)


def parse_arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Check if input array contains duplicates."
    )

    parser.add_argument(
        "--array",
        nargs="+",
        type=int,
        required=True,
        help="The integers to search.",
    )

    return parser.parse_args()


def main() -> None:
    args = parse_arguments()
    solution = Solution()

    print(solution.contains_duplicate(args.array))
    print(solution.contains_duplicate_opt(args.array))
    print(solution.contains_duplicate_py(args.array))


if __name__ == "__main__":
    main()
