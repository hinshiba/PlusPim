import tsParser from "@typescript-eslint/parser";
import tsPlugin from "@typescript-eslint/eslint-plugin";

export default [
	{
		ignores: ["out", "dist", "**/*.d.ts"],
	},
	{
		files: ["**/*.ts"],
		languageOptions: {
			parser: tsParser,
			ecmaVersion: 6,
			sourceType: "module",
		},
		plugins: {
			"@typescript-eslint": tsPlugin,
		},
		rules: {
			"@typescript-eslint/naming-convention": "warn",
			// @typescript-eslint/semi was removed in typescript-eslint v8
			"semi": "warn",
			"curly": "warn",
			"eqeqeq": "warn",
			"no-throw-literal": "warn",
		},
	},
];
