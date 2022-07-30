module.exports = {
    "env": {
        "browser": true,
        "commonjs": true,
        "es2021": true,
        "jest/globals": true
    },
    "extends": [
        "eslint:recommended",
        "plugin:react/recommended",
        "plugin:react-hooks/recommended",
        "plugin:@typescript-eslint/recommended",
        "prettier"
    ],
    "parser": "@typescript-eslint/parser",
    "parserOptions": {
        "ecmaFeatures": {
            "jsx": true
        },
        "ecmaVersion": "latest"
    },
    "plugins": [
        "react",
        "jest",
        "@typescript-eslint"
    ],
    "ignorePatterns": [
        "typescript/commands/**/*.ts",
        "typescript/common/**/*.ts",
        "typescript/durandalPlugins/**/*.ts",
        "typescript/models/**/*.ts",
        "typescript/viewmodels/**/*.ts",
        "typescript/widgets/**/*.ts",
        "typescript/transitions/**/*.ts"
    ],
    "rules": {
        "react/prop-types": "off",
        "@typescript-eslint/no-var-requires": "off",
        "react/jsx-no-target-blank": "off",
        "@typescript-eslint/triple-slash-reference": "off",
    },
    "settings": {
        "react": {
            "pragma": "React",
            "fragment": "Fragment",
            "version": "detect"
        }
    }
}
