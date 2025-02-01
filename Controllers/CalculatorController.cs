using Microsoft.AspNetCore.Mvc;
using CalculatorApp.Models;
using NCalc;

namespace CalculatorApp.Controllers
{
    public class CalculatorController(ILogger<CalculatorController> logger) : Controller
    {
        private static readonly Dictionary<string, CalculatorModel> _inMemoryState = [];
        private const string UserCookieKey = "UserIdentifier";
        private readonly ILogger<CalculatorController> _logger = logger;

        private string GetUserIdentifier()
        {
            if (Request.Cookies.TryGetValue(UserCookieKey, out var userId))
            {
                return userId;
            }

            userId = Guid.NewGuid().ToString();
            Response.Cookies.Append(UserCookieKey, userId, new CookieOptions
            {
                HttpOnly = true,
                IsEssential = true,
                Expires = DateTimeOffset.UtcNow.AddDays(7)
            });

            return userId;
        }

        public IActionResult Index()
        {
            var userId = GetUserIdentifier();
            var calculator = _inMemoryState.TryGetValue(userId, out CalculatorModel? value) ? value : new CalculatorModel();
            return View(calculator);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Calculate(string button)
        {
            var userId = GetUserIdentifier();
            _logger.LogInformation("Button pressed: {Button}, User ID: {UserId}", button, userId);

            var calculator = _inMemoryState.TryGetValue(userId, out CalculatorModel? value) ? value : new CalculatorModel();

            _logger.LogInformation("Before processing: Display={Display}, Result={Result}, Operation={Operation}, IsNewInput={IsNewInput}",
                calculator.Display, calculator.Result, calculator.Operation, calculator.IsNewInput);

            try
            {
                switch (button)
                {
                    case "0":
                    case "1":
                    case "2":
                    case "3":
                    case "4":
                    case "5":
                    case "6":
                    case "7":
                    case "8":
                    case "9":
                    case ".": // Handle dot button
                        HandleDigit(calculator, button);
                        break;

                    case "+":
                    case "-":
                    case "*":
                    case "/":
                        HandleOperation(calculator, button);
                        break;

                    case "=":
                        PerformCalculationBasedOnInputString(calculator);
                        break;

                    case "C":
                        Clear(calculator);
                        break;

                    case "Backspace":
                        Backspace(calculator);
                        break;

                    default:
                        _logger.LogWarning("Invalid button pressed: {Button}", button);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing button press: {Button}", button);
                calculator.Display = "Error: " + ex.Message;
            }

            _logger.LogInformation("After processing: Display={Display}, Result={Result}, Operation={Operation}, IsNewInput={IsNewInput}",
                calculator.Display, calculator.Result, calculator.Operation, calculator.IsNewInput);

            _inMemoryState[userId] = calculator;

            return View("Index", calculator);
        }

        private static void HandleDigit(CalculatorModel calculator, string digit)
        {
            if (calculator.IsNewInput)
            {
                calculator.Display = digit;
                calculator.CombinedDisplay += digit; // Append digit to CombinedDisplay
                calculator.IsNewInput = false;
            }
            else
            {
                // Prevent multiple dots in the same number
                if (digit == "." && calculator.Display.Contains('.'))
                {
                    return; // Ignore additional dots
                }

                calculator.Display += digit;
                calculator.CombinedDisplay += digit; // Append digit to CombinedDisplay
            }
        }

        private static void  HandleOperation(CalculatorModel calculator, string operation)
        {
            if (!calculator.IsNewInput)
            {
                PerformCalculation(calculator);
            }

            // Store the current display value as the result for the next calculation
            calculator.Result = double.Parse(calculator.Display);

            // Update the operation
            calculator.Operation = operation;

            // Set IsNewInput to true to indicate the next input will be a new number
            calculator.IsNewInput = true;

            // Append operator to CombinedDisplay
            calculator.CombinedDisplay += operation;
        }

        private static void PerformCalculation(CalculatorModel calculator)
        {
            double currentNumber = double.Parse(calculator.Display);

            switch (calculator.Operation)
            {
                case "+":
                    calculator.Result += currentNumber;
                    break;
                case "-":
                    calculator.Result -= currentNumber;
                    break;
                case "*":
                    calculator.Result *= currentNumber;
                    break;
                case "/":
                    calculator.Result /= currentNumber;
                    break;
            }

            // Update the display with the result of the calculation
            calculator.Display = calculator.Result.ToString();

            // Clear the operation after performing the calculation
            calculator.Operation = string.Empty;

            // Indicate that the next input will be a new number
            calculator.IsNewInput = true;
        }

        private void PerformCalculationBasedOnInputString(CalculatorModel calculator)
        {
            try
            {
                // Parse the CombinedDisplay and evaluate the expression
                double result = EvaluateExpression(calculator.CombinedDisplay);

                // Update the display with the result
                calculator.Display = result.ToString();

                // Clear the CombinedDisplay after performing the calculation
                calculator.CombinedDisplay = result.ToString(); // Set CombinedDisplay to the result

                // Reset the state for the next calculation
                calculator.Result = result;
                calculator.Operation = string.Empty;
                calculator.IsNewInput = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error evaluating expression: {InputString}", calculator.CombinedDisplay);
                calculator.Display = "Invalid Expression"; // Display error message
                calculator.CombinedDisplay = "Invalid Expression"; // Clear CombinedDisplay
                calculator.Result = 0; // Reset Result
                calculator.Operation = string.Empty; // Reset Operation
                calculator.IsNewInput = true; // Reset IsNewInput
            }
        }

        private double EvaluateExpression(string expression)
        {
            try
            {
                // Use NCalc to evaluate the expression
                var result = new Expression(expression).Evaluate();
                return Convert.ToDouble(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error evaluating expression: {InputString}", expression);
                throw new InvalidOperationException("Invalid expression.", ex);
            }
        }

        private static void Clear(CalculatorModel calculator)
        {
            calculator.Display = "0";
            calculator.Result = 0;
            calculator.Operation = string.Empty;
            calculator.IsNewInput = true;
            calculator.CombinedDisplay = "";
        }

        private static void Backspace(CalculatorModel calculator)
        {
            if (calculator.CombinedDisplay.Length > 1)
            {
                // Remove the last character from CombinedDisplay
                calculator.CombinedDisplay = calculator.CombinedDisplay[..^1];
            }
            else
            {
                // If the display is "0", reset it to "0"
                calculator.CombinedDisplay = "0";
            }

            // If the display becomes empty after backspacing, reset IsNewInput to true
            if (string.IsNullOrEmpty(calculator.CombinedDisplay) || calculator.CombinedDisplay == "0")
            {
                calculator.IsNewInput = true;
            }
        }
    }
}