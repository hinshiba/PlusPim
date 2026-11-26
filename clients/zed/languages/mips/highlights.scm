(comment) @comment

[
  (macro_mnemonic)
  (integer_mnemonic)
  (float_mnemonic)
  (string_mnemonic)
  (control_mnemonic)
] @keyword

(instruction_mnemonic) @function

(register) @variable.special

[
  (label)
  (numeric_label)
  (symbol)
] @label

[
  (octal)
  (binary)
  (decimal)
  (hexadecimal)
  (float)
] @number

[
  (char)
  (string)
] @string

[
  ","
  "="
] @punctuation.delimiter

[
  "("
  ")"
] @punctuation.bracket
