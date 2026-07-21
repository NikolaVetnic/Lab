import argparse


class Solution:
    def two_sum(self, nums: list[int], target: int) -> list[int]:
        for i in range(len(nums) - 1):
            for j in range(i + 1, len(nums)):
                if (nums[i] + nums[j] == target):
                    return [i, j]
        
        return []

    def two_sum_hashmap(self, nums: list[int], target: int) -> list[int]:
        hashmap = {}

        for i in range(len(nums)):
            if nums[i] in hashmap:
                return [i, hashmap[nums[i]]]
            hashmap[target - nums[i]] = i

        return []


def parse_arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Find two array elements whose sum equals the target."
    )

    parser.add_argument(
        "--array",
        nargs="+",
        type=int,
        required=True,
        help="The integers to search.",
    )

    parser.add_argument(
        "--target",
        type=int,
        required=True,
        help="The desired sum."
    )

    return parser.parse_args()


def main() -> None:
    args = parse_arguments()
    solution = Solution()

    result = solution.two_sum(args.array, args.target)
    print(result)

    result_hashmap = solution.two_sum_hashmap(args.array, args.target)
    print(result)


if __name__ == "__main__":
    main()
