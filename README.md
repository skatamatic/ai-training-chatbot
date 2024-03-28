# ai-training-chatbot
The chatbot used to demonstrate API interactions

# setup
- You will need an [OpenAI API Key](https://platform.openai.com/api-keys)
- To use the weather service, you'll need [an api key as well](https://openweathermap.org/appid)
- You'll need Visual Studio 2022 and .net 7.0

# setting up functions
- You can uncomment the system functions to enable them [here](src/AI-Training-API/App.xaml.cs#L61)
- You can also extend the functions by implementing [FunctionBase](src/OpenAIAPI_Rystem/Functions/FunctionBase.cs) then adding the function to the service container
