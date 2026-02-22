import { DynamoDBClient, PutItemCommand, DeleteItemCommand } from '@aws-sdk/client-dynamodb';
import { CognitoJwtVerifier } from 'aws-jwt-verify';

const dynamo = new DynamoDBClient({});
const TABLE = process.env.TABLE_NAME;
const USER_POOL_ID = process.env.COGNITO_USER_POOL_ID;

const verifier = USER_POOL_ID
  ? CognitoJwtVerifier.create({ userPoolId: USER_POOL_ID, tokenUse: 'id' })
  : null;

export const connect = async (event) => {
  const connectionId = event.requestContext.connectionId;
  const token = event.queryStringParameters?.token;
  if (!token || !TABLE) {
    return { statusCode: 401, body: 'Unauthorized' };
  }
  let userId = '';
  let tenantId = '0';
  if (verifier) {
    try {
      const payload = await verifier.verify(token);
      userId = payload.sub ?? '';
      tenantId = payload['custom:tenant_id'] ?? '0';
    } catch {
      return { statusCode: 401, body: 'Invalid token' };
    }
  } else {
    const parts = token.split('.');
    if (parts.length === 3) {
      try {
        const b64 = parts[1].replace(/-/g, '+').replace(/_/g, '/');
        const payload = JSON.parse(Buffer.from(b64, 'base64').toString());
        userId = payload.sub ?? '';
        tenantId = payload['custom:tenant_id'] ?? '0';
      } catch {
        return { statusCode: 401, body: 'Invalid token' };
      }
    }
  }
  if (!userId) return { statusCode: 401, body: 'No user' };
  const userKey = `${tenantId}#${userId}`;
  await dynamo.send(
    new PutItemCommand({
      TableName: TABLE,
      Item: {
        connectionId: { S: connectionId },
        userKey: { S: userKey },
        tenantId: { S: tenantId },
        userId: { S: userId },
        connectedAt: { S: new Date().toISOString() },
      },
    })
  );
  return { statusCode: 200, body: 'Connected' };
};

export const disconnect = async (event) => {
  const connectionId = event.requestContext.connectionId;
  if (!TABLE) return { statusCode: 200, body: 'OK' };
  await dynamo.send(
    new DeleteItemCommand({
      TableName: TABLE,
      Key: { connectionId: { S: connectionId } },
    })
  );
  return { statusCode: 200, body: 'Disconnected' };
};

export const handler = async (event) => {
  const route = event.requestContext?.routeKey;
  if (route === '$connect') return connect(event);
  if (route === '$disconnect') return disconnect(event);
  return { statusCode: 200, body: 'OK' };
};
