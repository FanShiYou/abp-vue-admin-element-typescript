import qs from 'querystring'
import { PagedAndSortedResultRequestDto, FullAuditedEntityDto, PagedResultDto, ListResultDto, ExtensibleObject } from '@/api/types'
import { OrganizationUnit } from './organizationunit'
import ApiService from './serviceBase'

const IdentityServiceUrl = process.env.VUE_APP_BASE_API
const IdentityServerUrl = process.env.VUE_APP_BASE_IDENTITY_SERVER

export default class UserApiService {
  public static getUsers(input: UsersGetPagedDto) {
    let _url = '/api/identity/users'
    _url += '?skipCount=' + input.skipCount
    _url += '&maxResultCount=' + input.maxResultCount
    if (input.sorting) {
      _url += '&sorting=' + input.sorting
    }
    if (input.filter) {
      _url += '&filter=' + input.filter
    }
    return ApiService.Get<PagedResultDto<User>>(_url, IdentityServiceUrl)
  }

  public static getUserById(userId: string) {
    let _url = '/api/identity/users/'
    _url += userId
    return ApiService.Get<User>(_url, IdentityServiceUrl)
  }

  public static getUserByName(userName: string) {
    let _url = '/api/identity/users/by-username/'
    _url += userName
    return ApiService.Get<User>(_url, IdentityServiceUrl)
  }

  public static updateUser(userId: string, userData: UserUpdate) {
    let _url = '/api/identity/users/'
    _url += userId
    return ApiService.Put<User>(_url, userData, IdentityServiceUrl)
  }

  public static deleteUser(userId: string | undefined) {
    let _url = '/api/identity/users/'
    _url += userId
    return ApiService.Delete(_url, IdentityServiceUrl)
  }

  public static createUser(userData: UserCreate) {
    const _url = '/api/identity/users'
    return ApiService.Post<User>(_url, userData, IdentityServiceUrl)
  }

  public static getUserRoles(userId: string) {
    let _url = '/api/identity/users'
    _url += '/' + userId
    _url += '/roles'
    return ApiService.Get<UserRoleDto>(_url, IdentityServiceUrl)
  }

  public static getUserClaims(userId: string) {
    const _url = '/api/identity/users/' + userId + '/claims'
    return ApiService.Get<ListResultDto<UserClaim>>(_url, IdentityServiceUrl)
  }

  public static addUserClaim(userId: string, payload: UserClaimCreateOrUpdate) {
    const _url = '/api/identity/users/' + userId + '/claims'
    return ApiService.Post<void>(_url, payload, IdentityServiceUrl)
  }

  public static updateUserClaim(userId: string, payload: UserClaimCreateOrUpdate) {
    const _url = '/api/identity/users/' + userId + '/claims'
    return ApiService.Put<void>(_url, payload, IdentityServiceUrl)
  }

  public static deleteUserClaim(userId: string, payload: UserClaimDelete) {
    let _url = '/api/identity/users/' + userId + '/claims'
    _url += '?claimType=' + payload.claimType
    _url += '&claimValue=' + payload.claimValue
    return ApiService.Delete(_url, IdentityServiceUrl)
  }

  public static getOrganizationUnits(userId: string, payload: UsersGetPagedDto) {
    let _url = '/api/identity/users/' + userId
    _url += '/organization-units'
    _url += '?filter=' + payload.filter
    _url += '&sorting=' + payload.sorting
    _url += '&skipCount=' + payload.skipCount
    _url += '&maxResultCount=' + payload.maxResultCount
    return ApiService.Get<PagedResultDto<OrganizationUnit>>(_url, IdentityServiceUrl)
  }

  public static removeOrganizationUnit(userId: string, ouId: string) {
    const _url = '/api/identity/users/' + userId + '/organization-units/' + ouId
    return ApiService.Delete(_url, IdentityServiceUrl)
  }

  public static changeUserOrganizationUnits(userId: string, payload: ChangeUserOrganizationUnitDto) {
    const _url = '/api/identity/users/organization-units/' + userId
    return ApiService.Put<void>(_url, payload, IdentityServiceUrl)
  }

  public static setUserRoles(userId: string, roles: string[]) {
    let _url = '/api/identity/users'
    _url += '/' + userId
    _url += '/roles'
    return ApiService.HttpRequest({
      baseURL: IdentityServiceUrl,
      url: _url,
      data: { RoleNames: roles },
      method: 'PUT'
    })
  }

  public static changePassword(input: UserChangePasswordDto) {
    const _url = '/api/identity/my-profile/change-password'
    return ApiService.HttpRequest({
      baseURL: IdentityServiceUrl,
      url: _url,
      data: input,
      method: 'POST'
    })
  }

  public static resetPassword(input: UserResetPasswordData) {
    const _url = '/api/account/phone/reset-password'
    return ApiService.HttpRequest({
      baseURL: IdentityServiceUrl,
      url: _url,
      data: input,
      method: 'PUT'
    })
  }

  public static userRegister(registerData: UserRegisterData) {
    const _url = '/api/account/phone/register'
    return ApiService.HttpRequest<User>({
      baseURL: IdentityServiceUrl,
      url: _url,
      method: 'POST',
      data: registerData
    })
  }

  public static getUserInfo() {
    const _url = '/connect/userinfo'
    return ApiService.HttpRequest<UserInfo>({
      baseURL: IdentityServerUrl,
      url: _url,
      method: 'GET'
    })
  }

  public static userLogin(loginData: UserLoginData) {
    const _url = '/connect/token'
    const login = {
      grant_type: 'password',
      username: loginData.userName,
      password: loginData.password,
      client_id: process.env.VUE_APP_CLIENT_ID,
      client_secret: process.env.VUE_APP_CLIENT_SECRET
    }
    return ApiService.HttpRequest<UserLoginResult>({
      baseURL: IdentityServerUrl,
      url: _url,
      method: 'POST',
      data: qs.stringify(login),
      headers: {
        'Content-Type': 'application/x-www-form-urlencoded'
      }
    })
  }

  /** ??????????????????????????? */
  public static sendSmsSigninCode(phoneNumber: string) {
    const _url = '/api/account/phone/send-signin-code'
    return ApiService.HttpRequest<void>({
      baseURL: IdentityServiceUrl,
      url: _url,
      method: 'POST',
      data: {
        phoneNumber: phoneNumber
      }
    })
  }

  public static sendSmsResetPasswordCode(phoneNumber: string) {
    const _url = '/api/account/phone/send-password-reset-code'
    return ApiService.HttpRequest<void>({
      baseURL: IdentityServiceUrl,
      url: _url,
      method: 'POST',
      data: {
        phoneNumber: phoneNumber
      }
    })
  }

  public static sendSmsRegisterCode(phoneNumber: string) {
    const _url = '/api/account/phone/send-register-code'
    return ApiService.HttpRequest<void>({
      baseURL: IdentityServiceUrl,
      url: _url,
      method: 'POST',
      data: {
        phoneNumber: phoneNumber
      }
    })
  }

  public static userLoginWithPhone(loginData: UserLoginPhoneData) {
    const _url = '/connect/token'
    const login = {
      grant_type: 'phone_verify',
      phone_number: loginData.phoneNumber,
      phone_verify_code: loginData.verifyCode,
      client_id: process.env.VUE_APP_CLIENT_ID,
      client_secret: process.env.VUE_APP_CLIENT_SECRET
    }
    return ApiService.HttpRequest<UserLoginResult>({
      baseURL: IdentityServerUrl,
      url: _url,
      method: 'POST',
      data: qs.stringify(login),
      headers: {
        'Content-Type': 'application/x-www-form-urlencoded'
      }
    })
  }

  public static refreshToken(token: string, refreshToken: string) {
    const _url = '/connect/token'
    const refresh = {
      grant_type: 'refresh_token',
      refresh_token: refreshToken,
      client_id: process.env.VUE_APP_CLIENT_ID,
      client_secret: process.env.VUE_APP_CLIENT_SECRET
    }
    return ApiService.HttpRequest<UserLoginResult>({
      baseURL: IdentityServerUrl,
      url: _url,
      method: 'POST',
      data: qs.stringify(refresh),
      headers: {
        'Content-Type': 'application/x-www-form-urlencoded',
        Authorization: token
      }
    })
  }

  public static userLogout(token: string | undefined) {
    if (token) {
      const _url = '/connect/revocation'
      const loginOut = {
        token: token,
        client_id: process.env.VUE_APP_CLIENT_ID,
        client_secret: process.env.VUE_APP_CLIENT_SECRET
      }
      return ApiService.HttpRequestWithOriginResponse({
        baseURL: IdentityServerUrl,
        url: _url,
        method: 'post',
        data: qs.stringify(loginOut),
        headers: {
          'Content-Type': 'application/x-www-form-urlencoded'
        }
      })
    }
  }
}

/** ???????????????????????? */
export class UsersGetPagedDto extends PagedAndSortedResultRequestDto {
  /** ?????????????????? */
  filter = ''

  constructor() {
    super()
    this.sorting = 'name'
  }
}

/** ?????????????????? */
export class UserRegisterData {
  /** ???????????? */
  phoneNumber!: string
  /** ??????????????? */
  verifyCode!: string
  /** ?????? */
  name?: string
  /** ????????? */
  userName?: string
  /** ?????? */
  password!: string
  /** ???????????? */
  emailAddress!: string
}

/** ?????????????????? */
export class UserLoginData {
  /** ????????? */
  userName!: string
  /** ???????????? */
  password!: string
}

export enum VerifyType {
  Register = 0,
  Signin = 10,
  ResetPassword = 20
}

export class PhoneVerify {
  phoneNumber!: string
  verifyType!: VerifyType
}

export class UserResetPasswordData {
  /** ???????????? */
  phoneNumber!: string
  /** ??????????????? */
  verifyCode!: string
  /** ????????? */
  newPassword!: string
}

/** ???????????????????????? */
export class UserLoginPhoneData {
  /** ???????????? */
  phoneNumber!: string
  /** ??????????????? */
  verifyCode!: string
}

/** ?????????????????? ???IdentityServer?????? */
export class UserInfo {
  /** ?????? */
  sub!: string
  /** ?????? */
  name!: string
  /** ???????????? */
  email!: string
  /** ???????????? */
  phone_number!: number
}

/** ???????????????????????? */
export class UserLoginResult {
  /** ???????????? */
  access_token!: string
  /** ???????????? */
  expires_in!: number
  /** ???????????? */
  token_type!: string
  /** ???????????? */
  refresh_token!: string
}

/** ???????????????????????? */
export class UserChangePasswordDto {
  /** ???????????? */
  currentPassword!: string
  /** ????????? */
  newPassword!: string
}

/** ?????????????????? */
export class UserRoleDto {
  /** ???????????? */
  items: IUserRole[]

  constructor() {
    this.items = new Array<IUserRole>()
  }
}

/** ?????????????????? */
export class UserRole implements IUserRole {
  /** ?????? */
  id!: string
  /** ?????? */
  name!: string
  /** ?????????????????? */
  isDefault!: boolean
  /** ?????????????????? */
  isStatic!: boolean
  /** ?????????????????? */
  isPublic!: boolean
}

export class UserCreateOrUpdate extends ExtensibleObject {
  /** ????????? */
  name = ''
  /** ???????????? */
  userName = ''
  /** ???????????? */
  surname = ''
  /** ???????????? */
  email = ''
  /** ???????????? */
  phoneNumber = ''
  /** ???????????? */
  lockoutEnabled = false
  /** ???????????? */
  roleNames: string[] | null = null
  /** ?????? */
  password: string | null = null
}

/** ?????????????????? */
export class UserUpdate extends UserCreateOrUpdate {
  /** ???????????? */
  concurrencyStamp = ''
}

export class UserCreate extends UserCreateOrUpdate {
}

/** ???????????? */
export class User extends FullAuditedEntityDto implements IUser {
  /** ????????? */
  name = ''
  /** ???????????? */
  userName = ''
  /** ???????????? */
  surname = ''
  /** ???????????? */
  email = ''
  /** ???????????? */
  phoneNumber = ''
  /** ??????????????? */
  twoFactorEnabled = false
  /** ???????????? */
  lockoutEnabled = false
  /** ???????????? */
  id = ''
  /** ???????????? */
  tenentId? = ''
  /** ??????????????? */
  emailConfirmed = false
  /** ????????????????????? */
  phoneNumberConfirmed = false
  /** ?????????????????? */
  lockoutEnd?: Date = undefined
  /** ???????????? */
  concurrencyStamp = ''
}

/** ?????????????????? */
export interface IUser {
  /** ????????? */
  name: string
  /** ???????????? */
  userName: string
  /** ???????????? */
  surname?: string
  /** ???????????? */
  email: string
  /** ???????????? */
  phoneNumber?: string
  /** ??????????????? */
  twoFactorEnabled: boolean
  /** ???????????? */
  lockoutEnabled: boolean
}

/** ?????????????????? */
export interface IUserRole {
  /** ???????????? */
  id: string
  /** ???????????? */
  name: string
  /** ???????????? */
  isDefault: boolean
  /** ???????????? */
  isStatic: boolean
  /** ???????????? */
  isPublic: boolean
}

export class ChangeUserOrganizationUnitDto {
  organizationUnitIds = new Array<string>()

  public addOrganizationUnit(id: string) {
    this.organizationUnitIds.push(id)
  }

  public removeOrganizationUnit(id: string) {
    const index = this.organizationUnitIds.findIndex(ouId => ouId === id)
    this.organizationUnitIds.splice(index, 1)
  }
}

export class UserClaimCreateOrUpdate {
  claimType = ''
  claimValue = ''
}

export class UserClaimDelete {
  claimType = ''
  claimValue = ''
}

export class UserClaim extends UserClaimCreateOrUpdate {
  id!: string
}
