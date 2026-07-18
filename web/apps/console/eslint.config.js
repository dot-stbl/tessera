import tesseraConfig from '@tessera/eslint-config';

export default [
  ...tesseraConfig,
  {
    ignores: ['dist/**', 'node_modules/**', 'coverage/**', 'src/routeTree.gen.ts'],
  },
];