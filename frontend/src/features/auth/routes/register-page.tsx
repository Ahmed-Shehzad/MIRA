import { useEffect } from 'react';
import { useNavigate } from 'react-router-dom';

/**
 * Registration is handled by Cognito (admin or pre-signup).
 * Redirect to login.
 */
export function RegisterPage() {
  const navigate = useNavigate();

  useEffect(() => {
    navigate('/login', { replace: true });
  }, [navigate]);

  return null;
}
