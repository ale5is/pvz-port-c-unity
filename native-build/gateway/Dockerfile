FROM python:3.12-slim
ENV PYTHONDONTWRITEBYTECODE=1 PYTHONUNBUFFERED=1
WORKDIR /app
COPY native-build /app/native-build
ENV DATA_DIR=/data HOST=0.0.0.0 PORT=8080
VOLUME ["/data"]
EXPOSE 8080
CMD ["python3", "/app/native-build/gateway/mod_build_gateway.py"]
